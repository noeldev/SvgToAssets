using Svg;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;

namespace SvgToAssets
{
    internal class ConverterCommand : RootCommand
    {
        // Enum for resource options
        internal enum AssetsType
        {
            icon,   // Generate icon only
            assets, // Generate assets only
            all     // Generate both icon and assets (default)
        }

        // Create the root command
        public ConverterCommand() : base($"{VersionInfo.Description}")
        {
            // Argument for the SVG file path (required for both commands)
            // Note: .ExistingOnly() would prevent ParseSvgPath from being
            // called so it has to check whether the source file exists.
            var svgPathArgument = new Argument<FileInfo>(
                name: "svgpath",
                description: "The path to the SVG source file.",
                parse: ParseSvgPath)
            {
                Arity = ArgumentArity.ExactlyOne
            };

            // Dynamic Tab completions for files (.svg or .rsp)
            svgPathArgument.AddCompletions((ctx) => GetFileCompletions(ctx, ".svg"));

            // Option for the output directory with environment variable expansion
            var outputDirOption = new Option<DirectoryInfo?>(
                name: "--output",
                description: "The output directory where the assets will be generated.\nDefaults to source path if not specified.",
                parseArgument: ParseOutputDirectory)
            {
                Arity = ArgumentArity.ExactlyOne
            };

            // Add aliases using the AddAlias method
            outputDirOption.AddAlias("-out");
            outputDirOption.AddAlias("-o");

            // Option that defines the type(s) of assets to generate
            var assetsType = new Option<AssetsType>(
                ["--type", "-t"], 
                "Type of assets to generate."
            ).FromAmong(AssetsTypes);

            assetsType.SetDefaultValue(DefaultAssetsType);

            // Option that defines the category of assets to generate
            var assetCategory = new Option<AssetCategory>(
                ["--assets", "-a"], 
                "Category of assets to generate."
            ).FromAmong(AssetGroup.Categories);

            assetCategory.SetDefaultValue(AssetGroup.DefaultCategory);

            // Option that defines the type of icon to generate
            var iconFullSet = new Option<bool>(
                ["--icon-all", "-i"],
                "Generate an icon with all supported image formats.");

            // Option to organize assets in 'scale-*' folders
            var createFolders = new Option<bool>(
                ["--folders", "-f"], 
                "Store PNG assets in 'scale-*' folders.");

            // Add arguments and options
            AddArgument(svgPathArgument);
            AddOption(outputDirOption);
            AddOption(assetsType);
            AddOption(assetCategory);
            AddOption(iconFullSet);
            AddOption(createFolders);

            // Command handler
            this.SetHandler(async (svgPath, outputDir, assetsType, assetCategory, iconFullSet, folders) =>
            {
                var outputPath = outputDir?.FullName ?? svgPath.DirectoryName!;
                try
                {
                    var svgDocument = await LoadSvgDocument(svgPath.FullName);

                    if (assetsType == AssetsType.icon)
                    {
                        await GenerateIconAsync(svgDocument, outputPath, iconFullSet);
                    }
                    else if (assetsType == AssetsType.assets)
                    {
                        await GenerateAssetsAsync(svgDocument, outputPath, assetCategory, folders);
                    }
                    else // AssetsType.all
                    {                       
                        var iconTask = GenerateIconAsync(svgDocument, outputPath, iconFullSet);
                        var assetTask = GenerateAssetsAsync(svgDocument, outputPath, assetCategory, folders);
                        await Task.WhenAll(iconTask, assetTask);
                    }
                }
                catch (FileNotFoundException ex)
                {
                    Program.SafeWriteError($"Error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Program.SafeWriteError($"Error in assets generation: {ex.Message}");
                }
            }, svgPathArgument, outputDirOption, assetsType, assetCategory, iconFullSet, createFolders);
        }

        public static string[] AssetsTypes => [.. Enum.GetNames<AssetsType>()];

        public static AssetsType DefaultAssetsType => AssetsType.all;

        #region Parameter validation

        private static FileInfo ParseSvgPath(ArgumentResult result)
        {
            // Expand environment variables
            var path = Environment.ExpandEnvironmentVariables(result.Tokens[0].Value);

            // Check if the path is relative and create an absolute path if necessary
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }

            // Simplify the path to resolve "." and ".."
            path = Path.GetFullPath(path);

            var fileInfo = new FileInfo(path);

            // Check that the file exists
            if (!fileInfo.Exists)
            {
                result.ErrorMessage = $"File does not exist: '{path}'.";
            }
            // Check that the file has a valid extension
            else if (!fileInfo.Extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = $"Not a valid SVG file: '{fileInfo.Name}'.";
            }

            return fileInfo;
        }

        private static DirectoryInfo ParseOutputDirectory(ArgumentResult result)
        {
            var path = Environment.ExpandEnvironmentVariables(result.Tokens[0].Value);

            // Check if the path is relative and create an absolute path if necessary
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }

            // Simplify the path to resolve "." and ".."
            path = Path.GetFullPath(path);

            var directoryInfo = new DirectoryInfo(path);

            // Check if directory exists, if not, create it
            if (!directoryInfo.Exists)
            {
                Program.SafeWriteLine($"Creating output directory: {path}");

                try
                {
                    directoryInfo.Create();
                }
                catch (IOException ex)
                {
                    result.ErrorMessage = $"Error creating directory '{path}': {ex.Message}";
                }
            }

            return new DirectoryInfo(path);
        }

        #endregion

        #region Tab completions

        private static IEnumerable<CompletionItem> GetFileCompletions(CompletionContext context, string extension)
        {
            var wordToComplete = context.WordToComplete;
            var prefix = "";

            // Handle response files
            if (wordToComplete.StartsWith('@'))
            {
                wordToComplete = wordToComplete.TrimStart('@');
                prefix = "@";
                extension = ".rsp";
            }

            var currentDirectory = Directory.GetCurrentDirectory();

            if (string.IsNullOrEmpty(wordToComplete))
            {
                // Enumerate files with the specified extension
                foreach (var file in Directory.EnumerateFiles(currentDirectory, $"*{extension}", SearchOption.AllDirectories))
                {
                    var relativeFilePath = Path.GetRelativePath(currentDirectory, file);
                    yield return new CompletionItem(prefix + relativeFilePath);
                }
            }
        }

        #endregion

        #region Icon and Asset Generation

        // Function to load the SVG document
        private static async Task<SvgDocument> LoadSvgDocument(string svgPath)
        {
            try
            {
                // Ensure the file is an SVG
                if (!Path.GetExtension(svgPath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"'{Path.GetFileName(svgPath)}' is not an SVG file.");
                }

                Program.SafeWriteLine($"Using SVG file: {svgPath}");

                return await Task.Run(() => SvgDocument.Open(svgPath));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error opening SVG file: {ex.Message}");
            }
        }

        // Async function to generate icon
        private static async Task GenerateIconAsync(SvgDocument svgDocument, string outputDirectory, bool generateAll)
        {
            var icoPath = Path.Combine(outputDirectory, "AppIcon.ico");

            try
            {
                var iconGenerator = new IconGenerator(svgDocument);

                // Get and display image dimensions
                var imageSizes = IconGenerator.GetImageSizesAsString(generateAll);
                Program.SafeWriteLine($"Generating image sizes: {imageSizes}");

                // Generate icon
                await Task.Run(() => iconGenerator.GenerateIcon(icoPath, generateAll));

                Program.SafeWriteSuccess($"Icon file generated successfully at {icoPath}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating icon file: {ex.Message}");
            }
        }

        // Async function to generate assets
        static async Task GenerateAssetsAsync(SvgDocument svgDocument, string outputDirectory, AssetCategory assetCategory, bool createFolders)
        {
            var assetsGenerator = new AssetsGenerator(svgDocument, createFolders);

            try
            {
                Program.SafeWriteLine($"Generating assets of category: {assetCategory}.");

                await Task.Run(() => assetsGenerator.GenerateAssets(outputDirectory, assetCategory));

                Program.SafeWriteSuccess($"Assets generated successfully at {outputDirectory}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating asset files: {ex.Message}");
            }
        }

        #endregion
    }
}
