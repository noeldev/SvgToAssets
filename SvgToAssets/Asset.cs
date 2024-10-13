namespace SvgToAssets
{
    // Class to define each asset and its sizes
    internal class Asset(string baseName, AssetSize[] sizes, AssetRequirement[]? requirements = null)
    {
        public string BaseName { get; } = baseName;
        public AssetSize[] Sizes { get; } = sizes;
        public AssetRequirement[] Requirements { get; } = requirements ?? [new RequiredAsset()]; // Default to required if not provided
    }

    // Class representing an asset size
    internal class AssetSize
    {
        public int Width { get; }
        public int Height { get; }
        public int? Scale { get; }
        public int? TargetSize { get; }

        public AssetSize(int width, int height, int scale)
        {
            Width = width;
            Height = height;
            Scale = scale;
            TargetSize = null;
        }

        public AssetSize(int targetSize)
        {
            Width = targetSize;
            Height = targetSize;
            Scale = null;
            TargetSize = targetSize;
        }

        public bool IsTarget => TargetSize.HasValue;
    }

    // Abstract base class for asset requirements
    internal abstract class AssetRequirement(string suffix)
    {
        public enum Level
        {
            Mandatory,  // Mandatory assets
            Required,   // Required assets (includes mandatory assets)
            Optional,   // Optional assets
            All         // Both required and optional assets
        }

        public string Suffix { get; } = suffix;

        protected abstract Level RequirementLevel { get; }

        public bool IsRequirementLevel(Level level)
        {
            return 
                RequirementLevel == level || 
                (level == Level.All &&
                (RequirementLevel == Level.Required || RequirementLevel == Level.Optional));
        }

        public static string[] LevelsAsString => Enum.GetNames(typeof(Level)).Select(name => name.ToLower()).ToArray();
       
        public static string DefaultLevelAsString => LevelsAsString[0];

        public static Level Parse(string level)
        {
            if (Enum.TryParse<Level>(level, true, out var parsedLevel))
            {
                return parsedLevel;
            }

            throw new ArgumentException("Invalid asset requirement level.");
        }
    }

    // Class for mandatory assets
    internal class MandatoryAsset(string suffix = "") : AssetRequirement(suffix)
    {
        protected override Level RequirementLevel => Level.Mandatory;
    }

    // Class for required assets
    internal class RequiredAsset(string suffix = "") : AssetRequirement(suffix)
    {
        protected override Level RequirementLevel => Level.Required;
    }

    // Class for optional assets
    internal class OptionalAsset(string suffix = "") : AssetRequirement(suffix)
    {
        protected override Level RequirementLevel => Level.Optional;
    }
}
