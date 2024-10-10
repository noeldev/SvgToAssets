using Svg;

namespace SvgToAssets
{
    partial class Program
    {
        static void Main(string[] args)
        {
            ShowTitle();

            // Check for input parameters
            if (args.Length == 0)
            {
                ShowSyntax();
                return;
            }

            var svgPath = args[0];

            // Validate SVG file path
            if (!File.Exists(svgPath))
            {
                Console.WriteLine("Error: The specified SVG file does not exist.");
                return;
            }

            // Parsing other (optional) parameters
            var generateAll = false;
            var createFolders = false;
            var outputDirectory = string.Empty;

            for (var i = 1; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith('-'))
                {
                    var option = arg[1..]; // Remove leading dash (-)

                    // Using StartsWith to check partial matches
                    if ("all".StartsWith(option, StringComparison.OrdinalIgnoreCase))
                    {
                        // Generate all icon sizes in ICO file and all PNG assets
                        generateAll = true;
                    }
                    else if ("folders".StartsWith(option, StringComparison.OrdinalIgnoreCase))
                    {
                        // Store PNG asset in "scale-*" folders
                        createFolders = true;
                    }
                    else
                    {
                        Console.WriteLine($"Unknown option: {arg}");
                    }
                }
                else if (i == 1)
                {
                    // Validate the specified output directory
                    outputDirectory = Path.GetFullPath(arg);

                    // Ensure the output directory exists
                    if (!Directory.Exists(outputDirectory))
                    {
                        Console.WriteLine($"Creating output directory...");
                        Directory.CreateDirectory(outputDirectory);
                    }
                }
            }

            if (string.IsNullOrEmpty(outputDirectory))
            {
                var svgDirectory = Path.GetDirectoryName(svgPath) ?? string.Empty;              
                outputDirectory = Path.GetFullPath(svgDirectory);
            }

            Console.WriteLine($"Using SVG file: {svgPath}");
            Console.WriteLine($"Output directory: {outputDirectory}");

            try
            {
                // Load the SVG file intended for asset generation
                var svgDocument = SvgDocument.Open(svgPath);

                try
                {
                    // Generate icon (ICO) file
                    var icoPath = Path.Combine(outputDirectory, "AppIcon.ico");
                    var icon = new IconGenerator(svgDocument);

                    icon.GenerateIcon(icoPath, generateAll);

                    Console.WriteLine($"ICO file generated successfully at {icoPath}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating ICO file: {ex.Message}");
                }

                try
                {
                    // Generate assets (PNG) files
                    var assets = new AssetsGenerator(svgDocument, createFolders);

                    assets.GenerateAssets(outputDirectory, generateAll);

                    Console.WriteLine("Assets generated successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error generating assets: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening SVG file: {ex.Message}");
                Environment.Exit(1); // Exit with a non-zero status to indicate failure
            }
        }

        static void ShowTitle()
        {
            var title = """
                SvgToAssets - Version 1.01
                Converts an SVG file to assets for WinUI projects.
                (c) 2024, Noël Danjou. All rights reserved.

                """;

            Console.WriteLine(title);
        }

        // Function to show syntax usage
        static void ShowSyntax()
        {
            var syntax = """
                Usage:
                  SvgToAssets <svgpath> [<outdir>] [-all][-folders]

                Where:
                    <svgpath>   The path to the SVG source file. (required)

                  Optional parameters:

                    [<outdir>]  The folder where output files will be generated.
                                If not specified, the source directory will be used.
                    [-all]      All possible assets and icons will be generated.
                    [-folders]  Assets will be organized in 'scale-*' subfolders.
                """;

            Console.WriteLine(syntax);
        }
    }
}
