using Svg;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace SvgToAssets
{
    // Syntax examples:

    // Generate both icon and asset files using folders:
    // SvgToAssets source.svg -o outdir -f

    // Generate a standard icon file to outdir:
    // SvgToAssets icon source.svg -o outdir

    // Generate asset files to outdir:
    // SvgToAssets assets source.svg -o outdir

    // Generate an icon file to the source folder:
    // SvgToAssets icon source.svg -a

    partial class Program
    {
        static async Task<int> Main(string[] args)
        {
            ShowTitle();

            // Argument for the SVG file path (required for both commands)
            var svgPathArgument = new Argument<FileInfo>(
                "svgpath",
                "The path to the SVG source file.")
            {
                Arity = ArgumentArity.ExactlyOne
            };

            // Option for the output directory
            var outputDirOption = new Option<DirectoryInfo>(
                ["--outdir", "-o"],
                "The output directory where the assets will be generated.\nIf not specified, the source directory will be used.");

            // Option to generate all possible image sizes
            var allOption = new Option<bool>(
                ["--all", "-a"],
                "Generate all possible image sizes."
            );

            // Option to organize assets in 'scale-*' folders
            var foldersOption = new Option<bool>(
                ["--folders", "-f"],
                "Store PNG assets in 'scale-*' folders."
            );

            // Create the root command and add arguments and options
            var rootCommand = new RootCommand
            {
                svgPathArgument,
                outputDirOption,
                allOption,
                foldersOption
            };

            // Description for root command
            rootCommand.Description = $"{VersionInfo.Description}";

            // Handler that runs both the icon and asset generation by default
            rootCommand.SetHandler(async (FileInfo svgpath, DirectoryInfo? outdir, bool all, bool folders) =>
            {
                // Common setup: Load SVG and check/create output directory
                var svgDocument = LoadSvgDocument(svgpath);
                var outputDirectory = PrepareOutputDirectory(outdir, svgpath);

                // Execute both the icon and asset generation
                await GenerateIconAsync(svgDocument, outputDirectory.FullName, all);
                await GenerateAssetsAsync(svgDocument, outputDirectory.FullName, all, folders);

            }, svgPathArgument, outputDirOption, allOption, foldersOption);

            // Add subcommand for generating only the icon
            var iconCommand = new Command("icon", "Converts an SVG file to an icon file.")
            {
                svgPathArgument,
                outputDirOption,
                allOption
            };

            // Handler for icon command
            iconCommand.SetHandler(async (FileInfo svgpath, DirectoryInfo? outdir, bool all) =>
            {
                // Common setup: Load SVG and check/create output directory
                var svgDocument = LoadSvgDocument(svgpath);
                var outputDirectory = PrepareOutputDirectory(outdir, svgpath);

                // Generate only the icon
                await GenerateIconAsync(svgDocument, outputDirectory.FullName, all);

            }, svgPathArgument, outputDirOption, allOption);

            // Add subcommand for generating only the assets
            var assetsCommand = new Command("assets", "Converts an SVG file to assets for WinUI projects.")
            {
                svgPathArgument,
                outputDirOption,
                allOption,
                foldersOption
            };

            // Handler for assets command
            assetsCommand.SetHandler(async (FileInfo svgpath, DirectoryInfo? outdir, bool all, bool folders) =>
            {
                // Common setup: Load SVG and check/create output directory
                var svgDocument = LoadSvgDocument(svgpath);
                var outputDirectory = PrepareOutputDirectory(outdir, svgpath);

                // Generate only the PNG assets
                await GenerateAssetsAsync(svgDocument, outputDirectory.FullName, all, folders);

            }, svgPathArgument, outputDirOption, allOption, foldersOption);

            // Add the subcommands to the root command
            rootCommand.AddCommand(iconCommand);
            rootCommand.AddCommand(assetsCommand);

            // Invoke the root command
            try
            {
                // Call the command with the parsed arguments
                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(1); // Exit with a non-zero status to indicate failure
                return 1;
            }
        }

        // Function to load the SVG document
        static SvgDocument LoadSvgDocument(FileInfo svgPath)
        {
            if (!svgPath.Exists)
            {
                throw new FileNotFoundException("The specified SVG file does not exist.", svgPath.FullName);
            }

            // Ensure the file is an SVG
            if (svgPath.Extension?.ToLower() != ".svg")
            {
                throw new Exception($"The file '{svgPath.Name}' is not an SVG.");
            }

            Console.WriteLine($"Using SVG file: {svgPath.FullName}");

            try
            {
                return SvgDocument.Open(svgPath.FullName);
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
                Console.WriteLine("Creating output directory...");

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

                Console.WriteLine($"Icon file generated successfully at {icoPath}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating icon file: {ex.Message}");
            }
        }

        // Async function to generate assets
        static async Task GenerateAssetsAsync(SvgDocument svgDocument, string outputDirectory, bool generateAll, bool createFolders)
        {
            var assetsGenerator = new AssetsGenerator(svgDocument, createFolders);

            try
            {
                await Task.Run(() => assetsGenerator.GenerateAssets(outputDirectory, generateAll)); // Assume GenerateAssets can be called async

                Console.WriteLine($"Assets generated successfully at {outputDirectory}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating asset files: {ex.Message}");
            }
        }

        static void ShowTitle()
        {
            var title = $"""
                SvgToAssets - Version {VersionInfo.Version}
                {VersionInfo.Copyright}
                """;

            Console.WriteLine(title);
        }
    }
}
