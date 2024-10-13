using Svg;
using System.Drawing;
using System.Drawing.Imaging;

namespace SvgToAssets
{
    internal class AssetsGenerator(SvgDocument svgDocument, bool createFolders)
    {
        private readonly SvgDocument _svgDocument = svgDocument;
        private readonly bool _createFolders = createFolders;

        // Main method to generate assets based on the generation type
        // cf. https://learn.microsoft.com/en-us/windows/uwp/app-resources/images-tailored-for-scale-theme-contrast#asset-size-tables
        public void GenerateAssets(string outputPath, string requirementLevel)
        {
            var level = AssetRequirement.Parse(requirementLevel);

            // Filter out assets according to level or requirement
            var assets = GetAssets().Where(asset => asset.Requirements.Any(req => req.IsRequirementLevel(level))).ToArray();

            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Loop through assets and generate files
            foreach (var asset in assets)
            {
                foreach (var size in asset.Sizes)
                {
                    // Generate the image for the current size
                    using Bitmap bmp = GenerateImage(size.Width, size.Height);

                    // Generate required/optional assets based on their requirements
                    foreach (var requirement in asset.Requirements)
                    {
                        if (requirement.IsRequirementLevel(level))
                        {
                            var outputFilePath = GetOutputFilePath(outputPath, asset.BaseName, size, requirement.Suffix);
                            bmp.Save(outputFilePath, ImageFormat.Png);
                        }
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
        private string GetOutputFilePath(string outputPath, string baseName, AssetSize size, string suffix)
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

            // Add suffix if present
            if (!string.IsNullOrEmpty(suffix))
            {
                fileName += $"_{suffix}";
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

        // cf. https://learn.microsoft.com/en-us/windows/apps/design/style/iconography/app-icon-construction#icon-sizes-wpf-uwp-winui

        private static Asset[] GetAssets() =>
        [
            new Asset("AppIcon",
            [
                // scale-*
                new (88, 88, 200),
            ],
            [
                // Requirements: scale-<size>
                new MandatoryAsset()
            ]),
            new Asset("AppIcon",
            [
                // targetsize-*
                new (16), new (24), new (32), new (48), new (256)
            ],
            [
                // Requirements: targetsize-<size>
                new MandatoryAsset()
            ]),
            new Asset("AppIcon",
            [
                // targetsize-*
                new (16), new (20), new (24), new (30), new (32),
                new (36), new (40), new (48), new (60), new (64),
                new (72), new (80), new (96), new (256)
            ],
            [
                // Requirements: targetsize-<size>[_<suffix>]
                new RequiredAsset(),
                new RequiredAsset("altform-unplated"),
                new RequiredAsset("altform-lightunplated")
            ]),
            new Asset("AppIcon",
            [
                // scale-*
                new (44, 44, 100), new (55, 55, 125),
                new (66, 66, 150), new (88, 88, 200),
                new (176, 176, 400)
            ],
            [
                // Requirements: scale-<size>[_<suffix>]
                new OptionalAsset(),
                new OptionalAsset("altform-colorful_theme-light")
            ]),
            new Asset("SmallTile",
            [
                new (71, 71, 100), new (89, 89, 125),
                new (107, 107, 150), new (142, 142, 200),
                new (284, 284, 400)
            ],
            [
                // Requirements: scale-<size>[_<suffix>]
                new RequiredAsset(),
                new OptionalAsset("altform-colorful_theme-light")
            ]),
            new Asset("MediumTile",
            [
                new (300, 300, 200)
            ],
            [
                // Requirements: scale-<size>
                new MandatoryAsset()
            ]),
            new Asset("MediumTile",
            [
                new (150, 150, 100), new (188, 188, 125),
                new (225, 225, 150), new (300, 300, 200),
                new (600, 600, 400)
            ],
            [
                // Requirements: scale-<size>[_<suffix>]
                new RequiredAsset(),
                new OptionalAsset("altform-colorful_theme-light")
            ]),
            new Asset("WideTile",
            [
                new (620, 300, 200)
            ],
            [
                // Requirements: scale-<size>
                new MandatoryAsset()
            ]),
            new Asset("WideTile",
            [
                new (310, 150, 100), new (388, 188, 125),
                new (465, 225, 150), new (620, 300, 200),
                new (1240, 600, 400)
            ],
            [
                // Requirements: scale-<size>[_<suffix>]
                new RequiredAsset(),
                new OptionalAsset("altform-colorful_theme-light")
            ]),
            new Asset("LargeTile",
            [
                new (310, 310, 100), new (388, 388, 125),
                new (465, 465, 150), new (620, 620, 200),
                new (1240, 1240, 400)
            ],
            [
                // Requirements: scale-<scale>[_<suffix>]
                new RequiredAsset(),
                new OptionalAsset("altform-colorful_theme-light")
            ]),
            new Asset("SplashScreen",
            [
                new (1240, 600, 200)
            ],
            [
                // Requirements: scale-<size>
                new MandatoryAsset()
            ]),
            new Asset("SplashScreen",
            [
                new (620, 300, 100), new (775, 375, 125),
                new (930, 450, 150), new (1240, 600, 200),
                new (2480, 1200, 400)
            ],
            [
                // Requirements: scale-<scale>[_<suffix>]
                new RequiredAsset(),
                new OptionalAsset("altform-colorful_theme-dark"),
                new OptionalAsset("altform-colorful_theme-light")
            ]),
            new Asset("BadgeLogo",
            [
                new (24, 24, 100), new (30, 30, 125),
                new (36, 36, 150), new (48, 48, 200),
                new (96, 96, 400)
            ],
            [
                // Requirements: scale-<scale>
                new OptionalAsset()
            ]),
            new Asset("StoreLogo",
            [
                new (50, 50, 100)
            ],
            [
                // Requirements: scale-<size>
                new MandatoryAsset()
            ]),
            new Asset("StoreLogo",
            [
                new (50, 50, 100), new (63, 63, 125),
                new (75, 75, 150), new (100, 100, 200)
            ],
            [
                // Requirements: scale-<scale>[_<suffix>]
                new RequiredAsset(),
                new OptionalAsset("altform-colorful_theme-light")
            ])
        ];
    }
}
