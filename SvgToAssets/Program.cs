using Svg;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Drawing;

namespace SvgToAssets
{
    // Syntax examples:

    // Generate an icon and asset files using folders:
    // SvgToAssets batch source.svg -o outdir -f

    // Generate a standard icon to outdir:
    // SvgToAssets icon source.svg -o outdir

    // Generate mandatory asset files to outdir:
    // SvgToAssets assets source.svg -o outdir

    // Generate required asset files to outdir:
    // SvgToAssets assets source.svg -o outdir -l required

    // Generate an icon to the source folder:
    // SvgToAssets icon source.svg -a

    partial class Program
    {
        private static readonly object _consoleLock = new();

        static async Task<int> Main(string[] args)
        {
            ShowTitle();

            // Argument for the SVG file path (required for both commands)
            var svgPathArgument = new Argument<FileInfo>(
                "svgpath",
                "The path to the SVG source file.")
            {
                Arity = ArgumentArity.ExactlyOne
            }.ExistingOnly(); // Ensure the file exists

            // Option for the output directory
            var outputDirOption = new Option<DirectoryInfo>(
                ["--outdir", "-o"],
                "The output directory where the assets will be generated.\nIf not specified, the source directory will be used.");

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
            ).FromAmong(AssetRequirement.LevelsAsString);

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
                await HandleIconCommand(svgpath, outdir, all);
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
                await HandleAssetsCommand(svgpath, outdir, requirement, folders);
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
                await HandleBatchCommand(svgpath, outdir, all, requirement, folders);
            }, svgPathArgument, outputDirOption, allOption, requirementLevelOption, foldersOption);

            // Create the root command and add subcommands
            var rootCommand = new RootCommand($"{VersionInfo.Description}");

            rootCommand.AddGlobalOption(outputDirOption);

            // Add the subcommands to the root command
            rootCommand.AddCommand(iconCommand);
            rootCommand.AddCommand(assetsCommand);
            rootCommand.AddCommand(batchCommand);

            // Invoke the root command
            try
            {
                // Call the command with the parsed arguments
                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                SafeConsoleWrite($"Error parsing command line: {ex.Message}");
                return 1;
            }
        }

        static async Task HandleIconCommand(FileInfo svgpath, DirectoryInfo? outdir, bool all)
        {
            try
            {
                var svgDocument = await LoadSvgDocument(svgpath);
                var outputDirectory = PrepareOutputDirectory(outdir, svgpath);
                await GenerateIconAsync(svgDocument, outputDirectory.FullName, all);
            }
            catch (FileNotFoundException ex)
            {
                SafeConsoleWrite($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                SafeConsoleWrite($"Error in icon generation: {ex.Message}");
            }
        }

        static async Task HandleAssetsCommand(FileInfo svgpath, DirectoryInfo? outdir, string requirement, bool folders)
        {
            try
            {
                var svgDocument = await LoadSvgDocument(svgpath);
                var outputDirectory = PrepareOutputDirectory(outdir, svgpath);
                await GenerateAssetsAsync(svgDocument, outputDirectory.FullName, requirement, folders);
            }
            catch (FileNotFoundException ex)
            {
                SafeConsoleWrite($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                SafeConsoleWrite($"Error in assets generation: {ex.Message}");
            }
        }

        static async Task HandleBatchCommand(FileInfo svgpath, DirectoryInfo? outdir, bool all, string requirement, bool folders)
        {
            try
            {
                var svgDocument = await LoadSvgDocument(svgpath);
                var outputDirectory = PrepareOutputDirectory(outdir, svgpath);
                
                // Run the task concurrently
                var iconTask = GenerateIconAsync(svgDocument, outputDirectory.FullName, all);
                var assetsTask = GenerateAssetsAsync(svgDocument, outputDirectory.FullName, requirement, folders);

                // Wait for the two tasks to complete
                await Task.WhenAll(iconTask, assetsTask);
            }
            catch (FileNotFoundException ex)
            {
                SafeConsoleWrite($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                SafeConsoleWrite($"Error in batch generation: {ex.Message}");
            }
        }

        // Function to load the SVG document
        static async Task<SvgDocument> LoadSvgDocument(FileInfo svgPath)
        {
            try
            {
                // Ensure the file is an SVG
                if (svgPath.Extension?.ToLower() != ".svg")
                {
                    throw new Exception($"'{svgPath.Name}' is not an SVG file.");
                }

                SafeConsoleWrite($"Using SVG file: {svgPath.FullName}");

                return await Task.Run(() => SvgDocument.Open(svgPath.FullName));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error opening SVG file: {ex.Message}");
            }
        }

        // Function to prepare the output directory
        static DirectoryInfo PrepareOutputDirectory(DirectoryInfo? outdir, FileInfo svgPath)
        {
            var outputDirectory = outdir ?? svgPath.Directory;

            if (!outputDirectory!.Exists)
            {
                SafeConsoleWrite("Creating output directory...");

                try
                {
                    Directory.CreateDirectory(outputDirectory.FullName);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error creating directory: {ex.Message}");
                }
            }

            return outputDirectory;
        }

        // Async function to generate icon
        static async Task GenerateIconAsync(SvgDocument svgDocument, string outputDirectory, bool generateAll)
        {
            var icoPath = Path.Combine(outputDirectory, "AppIcon.ico");

            try
            {
                var iconGenerator = new IconGenerator(svgDocument);

                await Task.Run(() => iconGenerator.GenerateIcon(icoPath, generateAll)); // Assume GenerateIcon can be called async

                SafeConsoleWrite($"Icon file generated successfully at {icoPath}.");
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
                SafeConsoleWrite($"Generating assets for requirement level: {requirement}.");

                await Task.Run(() => assetsGenerator.GenerateAssets(outputDirectory, requirement)); // Assume GenerateAssets can be called async

                SafeConsoleWrite($"Assets generated successfully at {outputDirectory}.");
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

        private static void SafeConsoleWrite(string message)
        {
            lock (_consoleLock)
            {
                Console.WriteLine(message);
            }
        }
    }
}
