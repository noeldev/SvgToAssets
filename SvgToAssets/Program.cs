using System.CommandLine;

namespace SvgToAssets
{
    partial class Program
    {
        private static readonly object _consoleLock = new();

        static async Task<int> Main(string[] args)
        {
            try
            {
                // Invoke the root command
                var rootCommand = new ConverterCommand();

                // Call the command with the parsed arguments
                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                SafeWriteError($"Error parsing command line: {ex.Message}");
                return 1;
            }
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
