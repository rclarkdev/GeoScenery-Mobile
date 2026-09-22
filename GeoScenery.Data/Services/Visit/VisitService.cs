using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Data.Services;

public sealed class VisitService : IVisitService
{
    private readonly MyProjectDbContext _dbContext;

    public VisitService(MyProjectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Visit>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var visits = await _dbContext.Visits
            .AsNoTracking()
            .Include(visit => visit.Scene)
            .ToListAsync(cancellationToken);

        return visits.OrderByDescending(visit => visit.VisitedAt).ToList();
    }

    public async Task<IReadOnlyList<Visit>> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        var visits = await _dbContext.Visits
            .AsNoTracking()
            .Include(visit => visit.Scene)
            .Where(visit => visit.UserId == userId)
            .ToListAsync(cancellationToken);

        return visits.OrderByDescending(visit => visit.VisitedAt).ToList();
    }

    public Task<Visit?> GetByIdAsync(long id, long userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Visits
            .AsNoTracking()
            .Include(visit => visit.Scene)
            .FirstOrDefaultAsync(visit => visit.Id == id && visit.UserId == userId, cancellationToken);
    }

    public async Task<Visit> CreateAsync(Visit visit, CancellationToken cancellationToken = default)
    {
        if (visit.VisitedAt == default)
        {
            visit.VisitedAt = DateTimeOffset.UtcNow;
        }

        _dbContext.Visits.Add(visit);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return visit;
    }

    public async Task<Visit?> UpdateAsync(long id, Visit visit, long userId, CancellationToken cancellationToken = default)
    {
        var existingVisit = await _dbContext.Visits
            .FirstOrDefaultAsync(existing => existing.Id == id && existing.UserId == userId, cancellationToken);
        if (existingVisit is null)
        {
            return null;
        }

        existingVisit.SceneId = visit.SceneId;
        existingVisit.VisitedAt = visit.VisitedAt;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return existingVisit;
    }

    public async Task<bool> DeleteAsync(long id, long userId, CancellationToken cancellationToken = default)
    {
        var visit = await _dbContext.Visits
            .FirstOrDefaultAsync(existing => existing.Id == id && existing.UserId == userId, cancellationToken);
        if (visit is null)
        {
            return false;
        }

        _dbContext.Visits.Remove(visit);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}