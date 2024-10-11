using Svg;
using System.Drawing;
using System.Drawing.Imaging;

namespace SvgToAssets
{
    internal class AssetsGenerator(SvgDocument svgDocument, bool createFolders)
    {
        private readonly SvgDocument _svgDocument = svgDocument;
        private readonly bool _createFolders = createFolders;
        
        // cf. https://learn.microsoft.com/en-us/windows/uwp/app-resources/images-tailored-for-scale-theme-contrast#asset-size-tables
        public void GenerateAssets(string outputPath, bool generateAll = false)
        {
            // List of assets to generate
            var assets = generateAll ? GetAllAssets() : GetBaseAssets();

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
                        // Save the same image under the dark theme's name
                        var darkThemeFilePath = GetDarkThemeFilePath(outputPath, asset.BaseName, size);
                        bmp.Save(darkThemeFilePath, ImageFormat.Png);

                        // Save the same image under the light theme's name
                        var lightThemeFilePath = GetLightThemeFilePath(outputPath, asset.BaseName, size);
                        bmp.Save(lightThemeFilePath, ImageFormat.Png);
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
            else if (!_createFolders)
            {
                fileName += $".scale-{size.Scale}";
            }

            // Add file extension
            fileName += ".png";

            // Determine if folders for scales should be used
            if (_createFolders && !size.IsTarget)
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

        // Function to generate output file path for Dark/Light assets
        private static string GetDarkThemeFilePath(string outputPath, string baseName, AssetSize size)
        {
            var fileName = $"{baseName}.targetsize-{size.TargetSize}_altform-unplated.png";
            return Path.Combine(outputPath, fileName);
        }

        private static string GetLightThemeFilePath(string outputPath, string baseName, AssetSize size)
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
                        // scale-*
                        new(88, 88, 200),

                        // targetsize-*
                        new(16), new(24), new(32), new(48), new(256)
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
                        new(16), new(20), new(24), new(30), new(32),
                        new(36), new(40), new(48), new(60), new(64),
                        new(72), new(80), new(96), new(256)
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
}
