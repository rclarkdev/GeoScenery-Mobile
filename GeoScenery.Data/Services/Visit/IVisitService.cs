using GeoScenery.Data.Models;

namespace GeoScenery.Data.Services;

public interface IVisitService
{
    Task<IReadOnlyList<Visit>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Visit>> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);
    Task<Visit?> GetByIdAsync(long id, long userId, CancellationToken cancellationToken = default);
    Task<Visit?> GetBySceneAsync(long userId, long sceneId, CancellationToken cancellationToken = default);
    Task<Visit> CreateAsync(Visit visit, CancellationToken cancellationToken = default);
    Task<Visit?> UpdateAsync(long id, Visit visit, long userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, long userId, CancellationToken cancellationToken = default);
}