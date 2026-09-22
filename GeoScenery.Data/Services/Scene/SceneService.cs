using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Data.Services;

public sealed class SceneService : ISceneService
{
    private const double EarthRadiusKm = 6371.0;

    private readonly MyProjectDbContext _dbContext;

    public SceneService(MyProjectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SceneSearchResult>> SearchAsync(
        IReadOnlyList<string>? tags,
        double? latitude,
        double? longitude,
        double? radiusKm,
        CancellationToken cancellationToken = default)
    {
        var normalizedTags = NormalizeTags(tags);

        var query = _dbContext.Scenes
            .AsNoTracking()
            .Include(scene => scene.Tags)
            .Include(scene => scene.Ratings)
            .AsQueryable();

        if (normalizedTags.Count > 0)
        {
            query = query.Where(scene => scene.Tags.Any(tag => normalizedTags.Contains(tag.Tag)));
        }

        var scenes = await query.OrderBy(scene => scene.Title).ToListAsync(cancellationToken);

        var hasLocationFilter = latitude.HasValue && longitude.HasValue;
        if (!hasLocationFilter)
        {
            return scenes.Select(scene => new SceneSearchResult(scene, null)).ToList();
        }

        var results = scenes
            .Select(scene => new SceneSearchResult(
                scene,
                scene.Latitude.HasValue && scene.Longitude.HasValue
                    ? GetDistanceKm(latitude!.Value, longitude!.Value, scene.Latitude.Value, scene.Longitude.Value)
                    : null))
            .Where(result => result.DistanceKm.HasValue && (!radiusKm.HasValue || result.DistanceKm <= radiusKm.Value))
            .OrderBy(result => result.DistanceKm)
            .ToList();

        return results;
    }

    public Task<Scene?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Scenes
            .AsNoTracking()
            .Include(scene => scene.Tags)
            .Include(scene => scene.Ratings)
            .FirstOrDefaultAsync(scene => scene.Id == id, cancellationToken);
    }

    public async Task<Scene> CreateAsync(Scene scene, IReadOnlyList<string>? tags, CancellationToken cancellationToken = default)
    {
        scene.CreatedAt = DateTimeOffset.UtcNow;
        scene.UpdatedAt = scene.CreatedAt;
        foreach (var tag in NormalizeTags(tags))
        {
            scene.Tags.Add(new SceneTag { Tag = tag });
        }

        _dbContext.Scenes.Add(scene);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return scene;
    }

    public async Task<Scene?> UpdateAsync(long id, Scene scene, long ownerUserId, IReadOnlyList<string>? tags, CancellationToken cancellationToken = default)
    {
        var existingScene = await _dbContext.Scenes
            .Include(existing => existing.Tags)
            .Include(existing => existing.Ratings)
            .FirstOrDefaultAsync(existing => existing.Id == id && existing.OwnerUserId == ownerUserId, cancellationToken);
        if (existingScene is null)
        {
            return null;
        }

        existingScene.Title = scene.Title;
        existingScene.Description = scene.Description;
        existingScene.ImageUrl = scene.ImageUrl;
        existingScene.Rating = scene.Rating;
        existingScene.Latitude = scene.Latitude;
        existingScene.Longitude = scene.Longitude;
        existingScene.UpdatedAt = DateTimeOffset.UtcNow;

        if (tags is not null)
        {
            var normalizedTags = NormalizeTags(tags);
            existingScene.Tags.Clear();
            foreach (var tag in normalizedTags)
            {
                existingScene.Tags.Add(new SceneTag { Tag = tag });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existingScene;
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct()
            .ToList();
    }

    private static double GetDistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var deltaLatitude = DegreesToRadians(latitude2 - latitude1);
        var deltaLongitude = DegreesToRadians(longitude2 - longitude1);

        var a = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2) +
                Math.Cos(DegreesToRadians(latitude1)) * Math.Cos(DegreesToRadians(latitude2)) *
                Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    public async Task<bool> DeleteAsync(long id, long ownerUserId, CancellationToken cancellationToken = default)
    {
        var scene = await _dbContext.Scenes
            .FirstOrDefaultAsync(existing => existing.Id == id && existing.OwnerUserId == ownerUserId, cancellationToken);
        if (scene is null)
        {
            return false;
        }

        _dbContext.Scenes.Remove(scene);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Scene?> RateAsync(long sceneId, long userId, decimal rating, CancellationToken cancellationToken = default)
    {
        var scene = await _dbContext.Scenes
            .Include(existing => existing.Ratings)
            .FirstOrDefaultAsync(existing => existing.Id == sceneId, cancellationToken);
        if (scene is null)
        {
            return null;
        }

        var existingRating = scene.Ratings.FirstOrDefault(sceneRating => sceneRating.UserId == userId);
        if (existingRating is not null)
        {
            existingRating.Rating = rating;
            existingRating.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            scene.Ratings.Add(new SceneRating { UserId = userId, Rating = rating });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return scene;
    }

    public async Task<bool> RemoveRatingAsync(long sceneId, long userId, CancellationToken cancellationToken = default)
    {
        var scene = await _dbContext.Scenes
            .Include(existing => existing.Ratings)
            .FirstOrDefaultAsync(existing => existing.Id == sceneId, cancellationToken);
        if (scene is null)
        {
            return false;
        }

        var existingRating = scene.Ratings.FirstOrDefault(sceneRating => sceneRating.UserId == userId);
        if (existingRating is not null)
        {
            scene.Ratings.Remove(existingRating);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}