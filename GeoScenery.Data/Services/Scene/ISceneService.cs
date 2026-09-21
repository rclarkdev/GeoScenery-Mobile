using GeoScenery.Data.Models;

namespace GeoScenery.Data.Services;

public interface ISceneService
{
    Task<IReadOnlyList<Scene>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Scene?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Scene> CreateAsync(Scene scene, CancellationToken cancellationToken = default);
    Task<Scene?> UpdateAsync(long id, Scene scene, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}