namespace SvgToAssets
{
    // Class to define each asset and its sizes
    internal class Asset(string baseName, AssetSize[] sizes, AssetGroup[]? groups = null)
    {
        public string BaseName { get; } = baseName;
        public AssetSize[] Sizes { get; } = sizes;
        public AssetGroup[] Groups { get; } = groups ?? [new RequiredAsset()]; // Default to required if not provided
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

    public enum AssetCategory
    {
        basic,      // Basic assets
        required,   // Required assets (includes basic assets)
        optional,   // Optional assets
        all         // Both required and optional assets
    }

    // Abstract base class for asset groups
    internal abstract class AssetGroup(string suffix)
    {
        public string Suffix { get; } = suffix;

        protected abstract AssetCategory Category { get; }

        public bool IsCategory(AssetCategory category)
        {
            return 
                Category == category || 
                (category == AssetCategory.all &&
                (Category == AssetCategory.required || Category == AssetCategory.optional));
        }

        public static string[] Categories => [.. Enum.GetNames<AssetCategory>()];

        public static AssetCategory DefaultCategory => AssetCategory.basic;
    }

    // Class for basic assets
    internal class BasicAsset(string suffix = "") : AssetGroup(suffix)
    {
        protected override AssetCategory Category => AssetCategory.basic;
    }

    // Class for required assets
    internal class RequiredAsset(string suffix = "") : AssetGroup(suffix)
    {
        protected override AssetCategory Category => AssetCategory.required;
    }

    // Class for optional assets
    internal class OptionalAsset(string suffix = "") : AssetGroup(suffix)
    {
        protected override AssetCategory Category => AssetCategory.optional;
    }
}
