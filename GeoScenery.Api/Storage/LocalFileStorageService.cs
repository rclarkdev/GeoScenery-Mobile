using System.Text.RegularExpressions;

namespace GeoScenery.Api.Storage;

/// <summary>
/// Stores uploaded images on the local filesystem and serves them from the
/// /uploads request path via static files. File names are always server generated
/// (random GUID + validated extension); client-supplied file names are never used
/// as paths, which prevents path traversal and executable uploads.
/// </summary>
public sealed partial class LocalFileStorageService(string rootPath) : IFileStorageService
{
    public string RootPath { get; } = Path.GetFullPath(rootPath);

    public async Task<string> SaveAsync(byte[] content, string fileExtension, CancellationToken cancellationToken = default)
    {
        var fileName = $"{Guid.NewGuid():N}.{fileExtension}";
        Directory.CreateDirectory(RootPath);
        var fullPath = Path.Combine(RootPath, fileName);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return $"/uploads/{fileName}";
    }

    public Task<bool> IsStoredImageAsync(string? requestPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return Task.FromResult(false);
        }

        var match = StoredImageReferenceRegex().Match(requestPath);
        if (!match.Success)
        {
            return Task.FromResult(false);
        }

        var fullPath = Path.GetFullPath(Path.Combine(RootPath, match.Groups["name"].Value));
        if (!fullPath.StartsWith(Path.GetFullPath(RootPath), StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(File.Exists(fullPath));
    }

    [GeneratedRegex("^/uploads/(?<name>[a-f0-9]{32}\\.(?:jpg|png|webp))$", RegexOptions.IgnoreCase)]
    private static partial Regex StoredImageReferenceRegex();
}