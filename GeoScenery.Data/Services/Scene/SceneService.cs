using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Data.Services;

public sealed class SceneService : ISceneService
{
    private readonly MyProjectDbContext _dbContext;

    public SceneService(MyProjectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Scene>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Scenes
            .AsNoTracking()
            .OrderBy(scene => scene.Title)
            .ToListAsync(cancellationToken);
    }

    public Task<Scene?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Scenes
            .AsNoTracking()
            .FirstOrDefaultAsync(scene => scene.Id == id, cancellationToken);
    }

    public async Task<Scene> CreateAsync(Scene scene, CancellationToken cancellationToken = default)
    {
        scene.CreatedAt = DateTimeOffset.UtcNow;
        scene.UpdatedAt = scene.CreatedAt;
        _dbContext.Scenes.Add(scene);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return scene;
    }

    public async Task<Scene?> UpdateAsync(long id, Scene scene, CancellationToken cancellationToken = default)
    {
        var existingScene = await _dbContext.Scenes.FindAsync([id], cancellationToken);
        if (existingScene is null)
        {
            return null;
        }

        existingScene.Title = scene.Title;
        existingScene.Description = scene.Description;
        existingScene.ImageUrl = scene.ImageUrl;
        existingScene.Rating = scene.Rating;
        existingScene.OwnerUserId = scene.OwnerUserId;
        existingScene.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return existingScene;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var scene = await _dbContext.Scenes.FindAsync([id], cancellationToken);
        if (scene is null)
        {
            return false;
        }

        _dbContext.Scenes.Remove(scene);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}