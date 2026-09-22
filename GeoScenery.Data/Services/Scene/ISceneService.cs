using GeoScenery.Data.Models;

namespace GeoScenery.Data.Services;

/// <summary>A scene paired with its distance from a search location, in kilometers (null when no location filter was applied).</summary>
public sealed record SceneSearchResult(Scene Scene, double? DistanceKm);

public interface ISceneService
{
    Task<IReadOnlyList<SceneSearchResult>> SearchAsync(
        IReadOnlyList<string>? tags,
        double? latitude,
        double? longitude,
        double? radiusKm,
        CancellationToken cancellationToken = default);
    Task<Scene?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Scene> CreateAsync(Scene scene, IReadOnlyList<string>? tags, CancellationToken cancellationToken = default);
    Task<Scene?> UpdateAsync(long id, Scene scene, long ownerUserId, IReadOnlyList<string>? tags, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, long ownerUserId, CancellationToken cancellationToken = default);
    Task<Scene?> RateAsync(long sceneId, long userId, decimal rating, CancellationToken cancellationToken = default);
    Task<bool> RemoveRatingAsync(long sceneId, long userId, CancellationToken cancellationToken = default);
}