namespace GeoScenery.Api.Storage;

/// <summary>
/// Abstraction over where uploaded image files live. The current implementation is
/// local filesystem storage served by the API; swapping in a cloud/object storage
/// provider later only requires a new implementation of this interface.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Absolute root directory where stored files are written.</summary>
    string RootPath { get; }

    /// <summary>
    /// Persists bytes under a newly generated file name and returns the public
    /// request path (e.g. <c>/uploads/&#123;guid&#125;.jpg</c>).
    /// </summary>
    Task<string> SaveAsync(byte[] content, string fileExtension, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when <paramref name="requestPath"/> is a valid server-hosted
    /// image reference that actually exists on disk. Arbitrary URLs, data URLs,
    /// and references to unknown/other files return false.
    /// </summary>
    Task<bool> IsStoredImageAsync(string? requestPath, CancellationToken cancellationToken = default);
}