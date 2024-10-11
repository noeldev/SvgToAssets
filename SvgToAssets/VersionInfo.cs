using System.Reflection;

namespace SvgToAssets
{
    internal static class VersionInfo
    {
        // Define a constant for undefined values
        private const string Undefined = "n/a";

        private static Assembly Assembly => Assembly.GetExecutingAssembly();

        // Property to get the version
        public static string Version => Assembly.GetName().Version?.ToString() ?? Undefined;

        // Property to get the authors
        public static string Authors => GetAssemblyAttribute<AssemblyMetadataAttribute>("Authors")?.Value ?? Undefined;

        // Property to get the copyright
        public static string Copyright => GetAssemblyAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? Undefined;

        // Property to get the product name
        public static string Product => GetAssemblyAttribute<AssemblyProductAttribute>()?.Product ?? Undefined;

        // Property to get the description
        public static string Description => GetAssemblyAttribute<AssemblyDescriptionAttribute>()?.Description ?? Undefined;

        // Generic helper to get assembly attributes
        private static T? GetAssemblyAttribute<T>(string propertyName = null) where T : Attribute
        {
            // Get all attributes of type T
            var attributes = Assembly.GetCustomAttributes(typeof(T), false);

            if (attributes.Length > 0)
            {
                // Special case for AssemblyMetadataAttribute to match the property name
                if (typeof(T) == typeof(AssemblyMetadataAttribute) && propertyName != null)
                {
                    foreach (var attribute in attributes)
                    {
                        var metadataAttribute = attribute as AssemblyMetadataAttribute;
                        if (metadataAttribute?.Key == propertyName)
                        {
                            return metadataAttribute as T;
                        }
                    }
                }
                else
                {
                    return attributes[0] as T;
                }
            }

            return null;
        }
    }
}
