using Svg;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SvgToAssets
{
    internal class AssetsGenerator(string svgPath)
    {
        private readonly SvgDocument _svgDocument = SvgDocument.Open(svgPath);

        public bool GenerateAllAssets { get; set; } = false;
        public bool CreateFolders { get; set; } = false;

        
        #region Icon Generation

        // cf. https://learn.microsoft.com/en-us/windows/apps/design/style/iconography/app-icon-construction
        public void GenerateIcon(string icoPath)
        {
            // Define standard sizes (base or all)
            int[] standardSizes = [16, 24, 32, 48, 256]; // Minimal required sizes
            int[] allSizes = [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256]; // All possible sizes

            // Choose the size set based on the generation mode
            var selectedSizes = GenerateAllAssets ? allSizes : standardSizes;

            try
            {
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
                    imageStream.Seek(0, SeekOrigin.Begin);
                    imageStream.CopyTo(iconStream);
                    imageStream.Dispose();
                }

                Console.WriteLine($"ICO file generated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating ICO file: {ex.Message}");
            }
        }

        static private MemoryStream SavePngIconImage(Bitmap bitmap)
        {
            // Save the image in memory
            var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream;
        }

        static private MemoryStream SaveBmpIconImage(Bitmap bitmap)
        {
            // Create bitmap (XOR mask) data
            using var xorMaskData = SaveBitmap(bitmap, out int xorMaskDataSize);

            // Create AND mask data
            using var andMaskData = CreateAndMask(bitmap, out var andMaskDataSize);

            // Prepare BITMAPINFOHEADER values
            int width = bitmap.Width;
            int height = bitmap.Height;
            int biHeight = height * 2; // Double the height for the AND mask
            int biSizeImage = (int)(xorMaskDataSize + andMaskDataSize);
            var imageDataSize = biSizeImage + 54; // BITMAPINFOHEADER + data

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
            writer.Write((int)40);             // biSize
            writer.Write((int)width);          // biWidth
            writer.Write((int)biHeight);       // biHeight (double the original height)
            writer.Write((short)1);            // biPlanes
            writer.Write((short)32);           // biBitCount
            writer.Write((int)0);              // biCompression
            writer.Write((int)biSizeImage);    // biSizeImage
            writer.Write((int)0);              // biXPelsPerMeter
            writer.Write((int)0);              // biYPelsPerMeter
            writer.Write((int)0);              // biClrUsed
            writer.Write((int)0);              // biClrImportant

            // Write the XOR mask
            xorMaskData.CopyTo(stream);

            // Write the AND mask
            andMaskData.CopyTo(stream);

            return stream;
        }

        static private MemoryStream SaveBitmap(Bitmap bitmap, out int dataSize)
        {
            // Save bitmap in memory
            var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Bmp);

            // Skip headers
            // Size of BITMAPFILEHEADER is 14 bytes
            // Size of BITMAPINFOHEADER is 40 bytes
            stream.Seek(54, SeekOrigin.Begin);

            dataSize = (int)(stream.Length - stream.Position);

            return stream;
        }

        static private MemoryStream CreateAndMask(Bitmap bitmap, out int dataSize)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var maskStride = ((width + 31) / 32) * 4; // 1bpp mask aligned on DWORD
            var stream = new MemoryStream();
            var maskData = new byte[maskStride];

            // Clear mask, ensure padding bytes are set to 0
            Array.Clear(maskData, 0, maskData.Length);

            // Lock bitmap bits for direct pixel access
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
            var totalBytes = bmpData.Stride * bitmap.Height;
            var pixelData = new byte[totalBytes];

            // Copy bitmap data to byte array
            Marshal.Copy(bmpData.Scan0, pixelData, 0, totalBytes);
            bitmap.UnlockBits(bmpData);

            // Process each line
            for (var y = 0; y < height; y++)
            {
                int alphaIndex = y * bmpData.Stride + 3;
                byte packedPixel = 0;
                byte bitmask = 0x80;

                for (var x = 0; x < width; x++, alphaIndex += bytesPerPixel)
                {
                    var alpha = pixelData[alphaIndex];
                    
                    if (alpha < 128)
                    {
                        packedPixel |= bitmask; // Mark transparent pixels
                    }
                    
                    bitmask >>= 1;

                    if (bitmask == 0)
                    {
                        maskData[x / 8] = packedPixel;
                        packedPixel = 0;
                        bitmask = 0x80;
                    }
                }

                // Write row data
                stream.Write(maskData, 0, maskData.Length);
            }

            dataSize = maskStride * height;

            return stream;
        }

        #endregion

        #region Asset Generation

        // cf. https://learn.microsoft.com/en-us/windows/uwp/app-resources/images-tailored-for-scale-theme-contrast#asset-size-tables
        public void GenerateAssets(string outputPath)
        {
            // List of assets to generate
            var assets = GenerateAllAssets ? GetAllAssets() : GetBaseAssets();

            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Loop through the assets and generate PNGs
            foreach (var asset in assets)
            {
                var isAppIcon = asset.BaseName == "AppIcon";

                foreach (var size in asset.Sizes)
                {
                    // Generate the image
                    using Bitmap bmp = GenerateImage(size.Width, size.Height);

                    // Determine the output path based on scale or targetsize
                    var outputFilePath = GetOutputFilePath(outputPath, asset.BaseName, size);

                    // Save the image to disk
                    bmp.Save(outputFilePath, ImageFormat.Png);

                    // For AppIcons, generate the additional altform-unplated asset (only save without re-generating)
                    if (isAppIcon && size.IsTarget)
                    {
                        var altformFilePath = GetAltformUnplatedFilePath(outputPath, asset.BaseName, size);

                        // Save the same image under the altform-unplated name
                        bmp.Save(altformFilePath, ImageFormat.Png);
                    }
                }
            }
        }

        // Function to generate a PNG image from the pre-loaded SVG document
        private Bitmap GenerateImage(int width, int height)
        {
            // Create a new bitmap with the specified dimensions
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                // Clear the background with transparency
                g.Clear(Color.Transparent);

                // Calculate scaling and position to center the SVG in the bitmap
                var scale = Math.Min((float)width / _svgDocument.Width, (float)height / _svgDocument.Height);
                var offsetX = (width - _svgDocument.Width * scale) / 2;
                var offsetY = (height - _svgDocument.Height * scale) / 2;

                // Apply transformations and render the SVG onto the bitmap
                g.TranslateTransform(offsetX, offsetY);
                g.ScaleTransform(scale, scale);
                _svgDocument.Draw(g);
            }

            return bmp;
        }

        // Function to generate output file path for the assets
        private string GetOutputFilePath(string outputPath, string baseName, AssetSize size)
        {
            var fileName = baseName;

            // Add appropriate suffix based on target size or scale
            if (size.IsTarget)
            {
                fileName += $".targetsize-{size.TargetSize}";
            }
            else if (!CreateFolders)
            {
                fileName += $".scale-{size.Scale}";
            }

            // Add file extension
            fileName += ".png";

            // Determine if folders for scales should be used
            if (CreateFolders && !size.IsTarget)
            {
                var scaleFolder = $"scale-{size.Scale}";
                var scalePath = Path.Combine(outputPath, scaleFolder);

                // Ensure the directory exists
                if (!Directory.Exists(scalePath))
                {
                    Directory.CreateDirectory(scalePath);
                }
                return Path.Combine(scalePath, fileName);
            }

            return Path.Combine(outputPath, fileName);
        }

        // Function to generate output file path for altform-unplated assets
        private static string GetAltformUnplatedFilePath(string outputPath, string baseName, AssetSize size)
        {
            var fileName = $"{baseName}.targetsize-{size.TargetSize}_altform-unplated.png";
            return Path.Combine(outputPath, fileName);
        }

        // Base assets when "-all" is NOT specified
        private static Asset[] GetBaseAssets()
        {
            return
                [
                    new Asset("MediumTile",
                    [
                        new(300, 300, 200)
                    ]),
                    new Asset("WideTile",
                    [
                        new(620, 300, 200)
                    ]),
                    new Asset("AppIcon",
                    [
                        new(88, 88, 200), new(24)
                    ]),
                    new Asset("SplashScreen",
                    [
                        new(1240, 600, 200)
                    ]),
                    new Asset("PackageLogo",
                    [
                        new(50, 50, 100)
                    ])
                ];
        }

        // All assets when "-all" is specified
        private static Asset[] GetAllAssets()
        {
            return
                [
                    new Asset("SmallTile",
                    [
                        new(71, 71, 100), new(89, 89, 125),
                        new(107, 107, 150), new(142, 142, 200),
                        new(284, 284, 400)
                    ]),
                    new Asset("MediumTile",
                    [
                        new(150, 150, 100), new(188, 188, 125),
                        new(225, 225, 150), new(300, 300, 200),
                        new(600, 600, 400)
                    ]),
                    new Asset("WideTile",
                    [
                        new(310, 150, 100), new(388, 188, 125),
                        new(465, 225, 150), new(620, 300, 200),
                        new(1240, 600, 400)
                    ]),
                    new Asset("LargeTile",
                    [
                        new(310, 310, 100), new(388, 388, 125),
                        new(465, 465, 150), new(620, 620, 200),
                        new(1240, 1240, 400)
                    ]),
                    new Asset("AppIcon",
                    [
                        // scale-*
                        new(44, 44, 100), new(55, 55, 125),
                        new(66, 66, 150), new(88, 88, 200),
                        new(176, 176, 400),

                        // targetsize-*
                        new(16), new(24)
                    ]),
                    new Asset("SplashScreen",
                    [
                        new(620, 300, 100), new(775, 375, 125),
                        new(930, 450, 150), new(1240, 600, 200),
                        new(2480, 1200, 400)
                    ]),
                    new Asset("BadgeLogo",
                    [
                        new(24, 24, 100), new(30, 30, 125),
                        new(36, 36, 150), new(48, 48, 200),
                        new(96, 96, 400)
                    ]),
                    new Asset("PackageLogo",
                    [
                        new(50, 50, 100), new(63, 63, 125),
                        new(75, 75, 150), new(100, 100, 200)
                    ])
                ];
        }
    }

    // Class to define each asset and its sizes
    internal class Asset(string baseName, AssetSize[] sizes)
    {
        public string BaseName { get; } = baseName;
        public AssetSize[] Sizes { get; } = sizes;
    }

    // Class representing an asset and its sizes
    internal class AssetSize
    {
        public AssetSize(int width, int height, int scale)
        {
            Width = width;
            Height = height;
            Scale = scale;
            TargetSize = null;
        }

        public AssetSize(int size)
        {
            Width = size;
            Height = size;
            Scale = 0;
            TargetSize = size;
        }

        public int Width { get; }
        public int Height { get; }
        public int Scale { get; }
        public int? TargetSize { get; }

        public bool IsTarget => Scale == 0;
    }

    #endregion
}
