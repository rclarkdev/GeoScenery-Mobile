using GeoScenery.Data.Models;

namespace GeoScenery.Data.Services;

public interface IVisitService
{
    Task<IReadOnlyList<Visit>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Visit>> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);
    Task<Visit?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Visit> CreateAsync(Visit visit, CancellationToken cancellationToken = default);
    Task<Visit?> UpdateAsync(long id, Visit visit, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}