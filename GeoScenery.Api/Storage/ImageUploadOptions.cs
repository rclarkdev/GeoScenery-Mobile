namespace GeoScenery.Api.Storage;

/// <summary>
/// Configuration for image upload validation and processing. Bound from the
/// "Storage" configuration section (e.g.
/// Storage:MaxUploadBytes, Storage:MaxDimensionPixels).
/// </summary>
public sealed class ImageUploadOptions
{
    public const long DefaultMaxUploadBytes = 5 * 1024 * 1024;
    public const int DefaultMaxDimensionPixels = 1600;
    public const int DefaultProfileMaxDimensionPixels = 512;

    public long MaxUploadBytes { get; set; } = DefaultMaxUploadBytes;
    public int MaxDimensionPixels { get; set; } = DefaultMaxDimensionPixels;
    public int ProfileMaxDimensionPixels { get; set; } = DefaultProfileMaxDimensionPixels;
    public LocalStorageOptions Local { get; set; } = new();
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; set; } = string.Empty;
}