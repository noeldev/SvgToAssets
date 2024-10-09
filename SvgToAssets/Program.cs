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
            var generateAllAssets = false;
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
                        generateAllAssets = true;
                    }
                    else if ("folders".StartsWith(option, StringComparison.OrdinalIgnoreCase))
                    {
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

            Console.WriteLine($"Output directory: {outputDirectory}");

            try
            {
                // Generate assets
                var assets = new AssetsGenerator(svgPath)
                {
                    // Configure generator
                    GenerateAllAssets = generateAllAssets,
                    CreateFolders = createFolders
                };

                // Generate icon file
                var icoPath = Path.Combine(outputDirectory, "AppIcon.ico");
                assets.GenerateIcon(icoPath);

                // Generate Assets (PNG) files
                assets.GenerateAssets(outputDirectory);

                // If the user requested all assets, generate additional assets
                Console.WriteLine("Assets generated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating assets: {ex.Message}");
            }
        }

        static void ShowTitle()
        {
            var title = """
                SvgToAssets - Version 1.0
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
