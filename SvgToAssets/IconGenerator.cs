using Svg;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;

namespace SvgToAssets
{
    internal class IconGenerator(SvgDocument svgDocument)
    {
        private readonly SvgDocument _svgDocument = svgDocument;

        // Return selected sizes for display
        public static int[] GetImageSizes(bool fullSet)
        {
            // Define standard sizes
            int[] standardSizes = [16, 24, 32, 48, 256]; // Minimal required sizes
            int[] allSizes = [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256]; // All possible sizes

            // Choose the size set based on the generation mode
            return fullSet ? allSizes : standardSizes;
        }

        // Return sizes as a comma-separated string
        public static string GetImageSizesAsString(bool fullSet)
        {
            var imageSizes = GetImageSizes(fullSet);
            return string.Join(", ", imageSizes.Select(size => $"{size}x{size}"));
        }
        
        // Generate icon from SVG
        // cf. https://learn.microsoft.com/en-us/windows/apps/design/style/iconography/app-icon-construction
        public void GenerateIcon(string icoPath, bool fullSet = false)
        {
            var selectedSizes = GetImageSizes(fullSet);

            // Generate the icon
            using var iconStream = new FileStream(icoPath, FileMode.Create);
            using var iconWriter = new BinaryWriter(iconStream);

            // Write the ICONDIR header (6 bytes)
            //  WORD idReserved;
            //  WORD idType;
            //  WORD idCount;
            //  ICONDIRENTRY idEntries[1];
            iconWriter.Write((short)0);   // Reserved (must be 0)
            iconWriter.Write((short)1);   // Resource Type (1 for icons)
            iconWriter.Write((short)selectedSizes.Length); // Number of images in the file

            // Write the ICONDIRENTRY headers (16 bytes per header)
            // BYTE bWidth;             Width, in pixels, of the image
            // BYTE bHeight;            Height, in pixels, of the image
            // BYTE bColorCount;        Number of colors in image (0 if >=8bpp)
            // BYTE bReserved;          Reserved ( must be 0)
            // WORD wPlanes;            Color Planes
            // WORD wBitCount;          Bits per pixel (bpp)
            // DWORD dwBytesInRes;      How many bytes in this resource?
            // DWORD dwImageOffset;     Where in the file is this image?
            var imageDataOffset = 6 + (selectedSizes.Length * 16); // Offset starts after headers
            var imageStreams = new List<MemoryStream>();

            // Write the payload (image data)
            foreach (var size in selectedSizes)
            {
                // Render the SVG document to the specified size
                _svgDocument.Width = size;
                _svgDocument.Height = size;

                // Create a 32bpp bitmap image with alpha channel
                using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                _svgDocument.Draw(bitmap);

                // Save the image in memory
                MemoryStream imageStream;

                if (size >= 256)
                {
                    // For larger sizes, compress as PNG
                    imageStream = SavePngIconImage(bitmap);
                }
                else
                {
                    // For smaller sizes, use uncompressed BMP
                    imageStream = SaveBmpIconImage(bitmap);
                }

                var imageDataSize = (int)imageStream.Length;

                // Write the ICONDIRENTRY structure
                iconWriter.Write((byte)size);       // Width (0 means 256-pixel wide image).
                iconWriter.Write((byte)size);       // Height (0 means 256-pixel high image).
                iconWriter.Write((byte)0);          // Color palette (0 means no palette).
                iconWriter.Write((byte)0);          // Reserved. Should be 0.
                iconWriter.Write((short)1);         // Color planes (always 1).
                iconWriter.Write((short)32);        // Bits per pixel.
                iconWriter.Write(imageDataSize);    // Size of the image data.
                iconWriter.Write(imageDataOffset);  // Offset of the image data.

                // Update offset for the next image
                imageDataOffset += imageDataSize;

                imageStreams.Add(imageStream);
            }

            // Write the payload (image data)
            foreach (var imageStream in imageStreams)
            {
                using (imageStream)
                {
                    imageStream.Seek(0, SeekOrigin.Begin);
                    imageStream.CopyTo(iconStream);
                }
            }
        }

        private static MemoryStream SavePngIconImage(Bitmap bitmap)
        {
            // Save the image in memory
            var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream;
        }

        private static MemoryStream SaveBmpIconImage(Bitmap bitmap)
        {
            // Create bitmap (XOR mask) and AND mask
            var (xorMask, andMask) = CreateMasks(bitmap);

            // Prepare BITMAPINFOHEADER values
            int biWidth = bitmap.Width;
            int biHeight = bitmap.Height * 2; // Double the height for the AND mask
            int biSizeImage = (int)(xorMask.Length + andMask.Length);

            // Save the image in memory
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);

            // Write the BITMAPINFOHEADER structure
            //  DWORD biSize;           Size in bytes of the structure (40 bytes).
            //  LONG biWidth;           Width of the bitmap, in pixels.
            //  LONG biHeight;          Height of the bitmap, in pixels.
            //  WORD biPlanes;          Number of planes (Must be 1).
            //  WORD biBitCount;        Number of bits per pixel (bpp).
            //  DWORD biCompression;    BI_RGB (0) for uncompressed RGB
            //  DWORD biSizeImage;      Size, in bytes, of the image
            //  LONG biXPelsPerMeter;   Not used (Must be 0)
            //  LONG biYPelsPerMeter;   Not used (Must be 0)
            //  DWORD biClrUsed;        Not used (Must be 0)
            //  DWORD biClrImportant;   Not used (Must be 0)
            writer.Write((int)40);
            writer.Write((int)biWidth);
            writer.Write((int)biHeight);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write((int)0);
            writer.Write((int)biSizeImage);
            writer.Write((int)0);
            writer.Write((int)0);
            writer.Write((int)0);
            writer.Write((int)0);

            // Write the XOR mask
            writer.Write(xorMask);

            // Write the AND mask
            writer.Write(andMask);

            return stream;
        }

        private static (byte[] xorMask, byte[] andMask) CreateMasks(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            // Lock the bitmap to access pixel data
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap.PixelFormat);

            // Declare an array to hold the bytes of the bitmap (XOR mask)
            var bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
            var bytes = Math.Abs(bmpData.Stride) * height;
            var rawData = new byte[bytes];

            // Copy pixel data to the rawData array
            Marshal.Copy(bmpData.Scan0, rawData, 0, rawData.Length);

            // Convert the memory format (Top-Down) to file format (Bottom-Up)
            var xorMask = ConvertTopDownToBottomUp(rawData, bmpData);

            // Declare an array to hold the bytes of the AND mask
            var maskStride = ((width + 31) / 32) * 4; // 1bpp mask aligned on DWORD
            var andMask = new byte[maskStride * height];

            // Clear mask, ensure padding bytes are set to 0
            Array.Clear(andMask, 0, andMask.Length);

            // Process each line
            var alphaStride = bmpData.Stride;

            for (var y = 0; y < height; y++)
            {
                var alphaIndex = y * alphaStride + 3; // BGRA
                var maskIndex = y * maskStride;
                byte packedPixel = 0;
                byte bitmask = 0x80;

                for (var x = 0; x < width; x++)
                {
                    var alpha = xorMask[alphaIndex];

                    if (alpha < 128)
                    {
                        packedPixel |= bitmask; // Mark transparent pixels
                    }

                    bitmask >>= 1;

                    if (bitmask == 0)
                    {
                        andMask[maskIndex++] = packedPixel;
                        packedPixel = 0;
                        bitmask = 0x80;
                    }

                    alphaIndex += bytesPerPixel;
                }

                // If there are remaining bits in the packed pixel
                if (bitmask != 0x80)
                {
                    andMask[maskIndex] = packedPixel;
                }
            }

            // Unlock the bitmap
            bitmap.UnlockBits(bmpData);

            return (xorMask, andMask);
        }

        private static byte[] ConvertTopDownToBottomUp(byte[] topDownMask, BitmapData info)
        {
            // Create an array to hold the bottom-up mask
            var bottomUpMask = new byte[topDownMask.Length];

            // Iterate through each row in reverse order
            var height = info.Height;
            var stride = info.Stride;
            var srcIndex = (height - 1) * stride;
            var destIndex = 0;

            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(topDownMask, srcIndex, bottomUpMask, destIndex, stride);
                srcIndex -= stride;
                destIndex += stride;
            }

            return bottomUpMask;
        }
    }
}
