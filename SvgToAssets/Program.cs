using Svg;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace SvgToAssets
{
    partial class Program
    {
        private static readonly object _consoleLock = new();

        static async Task<int> Main(string[] args)
        {
            ShowTitle();

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

            // Option for the output directory with environment variable expansion
            var outputDirOption = new Option<DirectoryInfo?>(
                name: "--out",
                description: "The output directory where the assets will be generated. This option is required if specified.",
                parseArgument: ParseOutputDirectory);

            // Add aliases using the AddAlias method
            outputDirOption.AddAlias("-o");

            // Option to generate all possible image sizes in icons
            var allOption = new Option<bool>(
                ["--all", "-a"],
                "Generate all supported image sizes."
            );

            // Option to organize assets in 'scale-*' folders
            var foldersOption = new Option<bool>(
                ["--folders", "-f"],
                "Store PNG assets in 'scale-*' folders."
            );

            // Option for asset requirement level
            var requirementLevelOption = new Option<string>(
                ["--level", "-l"],
                "Specify the asset requirement level."
            ).FromAmong(AssetRequirement.LevelsAsStrings);

            requirementLevelOption.SetDefaultValue(AssetRequirement.DefaultLevelAsString);

            // Subcommand for generating only the icon
            var iconCommand = new Command("icon", "Converts an SVG file into an icon.")
            {
                svgPathArgument,
                allOption
            };

            // Handler for icon command
            iconCommand.SetHandler(async (FileInfo svgpath, DirectoryInfo? outdir, bool all) =>
            {
                var outputPath = outdir?.FullName ?? svgpath.DirectoryName!;
                await HandleIconCommand(svgpath.FullName, outputPath, all);
            }, svgPathArgument, outputDirOption, allOption);

            // Subcommand for generating only the assets
            var assetsCommand = new Command("assets", "Converts an SVG file into asset files.")
            {
                svgPathArgument,
                requirementLevelOption,
                foldersOption
            };

            // Handler for assets command
            assetsCommand.SetHandler(async (FileInfo svgpath, DirectoryInfo? outdir, string requirement, bool folders) =>
            {
                var outputPath = outdir?.FullName ?? svgpath.DirectoryName!;
                await HandleAssetsCommand(svgpath.FullName, outputPath, requirement, folders);
            }, svgPathArgument, outputDirOption, requirementLevelOption, foldersOption);

            // Subcommand for generating both icon and assets
            var batchCommand = new Command("batch", "Converts an SVG file into an icon and asset files.")
            {
                svgPathArgument,
                allOption,
                requirementLevelOption,
                foldersOption
            };

            // Handler that runs both the icon and asset generation by default
            batchCommand.SetHandler(async (FileInfo svgpath, DirectoryInfo? outdir, bool all, string requirement, bool folders) =>
            {
                var outputPath = outdir?.FullName ?? svgpath.DirectoryName!;
                await HandleBatchCommand(svgpath.FullName, outputPath, all, requirement, folders);
            }, svgPathArgument, outputDirOption, allOption, requirementLevelOption, foldersOption);

            // Create the root command and add subcommands
            var rootCommand = new RootCommand($"{VersionInfo.Description}")
            {
                iconCommand,
                assetsCommand,
                batchCommand
            };

            rootCommand.AddGlobalOption(outputDirOption);

            // Invoke the root command
            try
            {
                // Call the command with the parsed arguments
                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                SafeWriteError($"Error parsing command line: {ex.Message}");
                return 1;
            }
        }

        static async Task HandleIconCommand(string svgPath, string outputPath, bool all)
        {
            try
            {
                var svgDocument = await LoadSvgDocument(svgPath);
                await GenerateIconAsync(svgDocument, outputPath, all);
            }
            catch (FileNotFoundException ex)
            {
                SafeWriteError($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                SafeWriteError($"Error in icon generation: {ex.Message}");
            }
        }

        static async Task HandleAssetsCommand(string svgPath, string outputPath, string requirement, bool folders)
        {
            try
            {
                var svgDocument = await LoadSvgDocument(svgPath);
                await GenerateAssetsAsync(svgDocument, outputPath, requirement, folders);
            }
            catch (FileNotFoundException ex)
            {
                SafeWriteError($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                SafeWriteError($"Error in assets generation: {ex.Message}");
            }
        }

        static async Task HandleBatchCommand(string svgPath, string outputPath, bool all, string requirement, bool folders)
        {
            try
            {
                var svgDocument = await LoadSvgDocument(svgPath);
                
                // Run the task concurrently
                var iconTask = GenerateIconAsync(svgDocument, outputPath, all);
                var assetsTask = GenerateAssetsAsync(svgDocument, outputPath, requirement, folders);

                // Wait for the two tasks to complete
                await Task.WhenAll(iconTask, assetsTask);
            }
            catch (FileNotFoundException ex)
            {
                SafeWriteError($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                SafeWriteError($"Error in batch generation: {ex.Message}");
            }
        }

        // Function to load the SVG document
        static async Task<SvgDocument> LoadSvgDocument(string svgPath)
        {
            try
            {
                // Ensure the file is an SVG
                if (!Path.GetExtension(svgPath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"'{Path.GetFileName(svgPath)}' is not an SVG file.");
                }

                SafeWriteLine($"Using SVG file: {svgPath}");

                return await Task.Run(() => SvgDocument.Open(svgPath));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error opening SVG file: {ex.Message}");
            }
        }

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

        static DirectoryInfo ParseOutputDirectory(ArgumentResult result)
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
                SafeWriteLine($"Creating output directory: {path}");

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

        // Async function to generate icon
        static async Task GenerateIconAsync(SvgDocument svgDocument, string outputDirectory, bool generateAll)
        {
            var icoPath = Path.Combine(outputDirectory, "AppIcon.ico");

            try
            {
                var iconGenerator = new IconGenerator(svgDocument);

                // Get and display image dimensions
                var imageSizes = IconGenerator.GetImageSizesAsString(generateAll);
                SafeWriteLine($"Generating image sizes: {imageSizes}");

                // Generate icon
                await Task.Run(() => iconGenerator.GenerateIcon(icoPath, generateAll)); // Assume GenerateIcon can be called async

                SafeWriteSuccess($"Icon file generated successfully at {icoPath}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating icon file: {ex.Message}");
            }
        }

        // Async function to generate assets
        static async Task GenerateAssetsAsync(SvgDocument svgDocument, string outputDirectory, string requirement, bool createFolders)
        {
            var assetsGenerator = new AssetsGenerator(svgDocument, createFolders);

            try
            {
                SafeWriteLine($"Generating assets for requirement level: {requirement}.");

                await Task.Run(() => assetsGenerator.GenerateAssets(outputDirectory, requirement));

                SafeWriteSuccess($"Assets generated successfully at {outputDirectory}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating asset files: {ex.Message}");
            }
        }

        static void ShowTitle()
        {
            var title = $"""
                {VersionInfo.Product} - Version {VersionInfo.Version}
                {VersionInfo.Copyright}

                """;

            Console.WriteLine(title);
        }

        public static void SafeWriteLine(string message, ConsoleColor? color = null)
        {
            lock (_consoleLock)
            {
                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                    Console.WriteLine(message);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(message);
                }
            }
        }

        public static void SafeWriteError(string message)
        {
            SafeWriteLine(message, ConsoleColor.Red);
        }

        public static void SafeWriteSuccess(string message)
        {
            SafeWriteLine(message, ConsoleColor.Green);
        }
    }
}
