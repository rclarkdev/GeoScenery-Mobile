using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Data.Services;

public sealed class FollowService : IFollowService
{
    private readonly MyProjectDbContext _dbContext;

    public FollowService(MyProjectDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsFollowingAsync(long followerId, long followingId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Follows
            .AsNoTracking()
            .AnyAsync(follow => follow.FollowerId == followerId && follow.FollowingId == followingId, cancellationToken);
    }

    public Task<int> GetFollowerCountAsync(long userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Follows.AsNoTracking().CountAsync(follow => follow.FollowingId == userId, cancellationToken);
    }

    public Task<int> GetFollowingCountAsync(long userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Follows.AsNoTracking().CountAsync(follow => follow.FollowerId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetFollowersAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Follows
            .AsNoTracking()
            .Where(follow => follow.FollowingId == userId)
            .OrderBy(follow => follow.Follower.DisplayName)
            .Select(follow => follow.Follower)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetFollowingAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Follows
            .AsNoTracking()
            .Where(follow => follow.FollowerId == userId)
            .OrderBy(follow => follow.Following.DisplayName)
            .Select(follow => follow.Following)
            .ToListAsync(cancellationToken);
    }

    public async Task FollowAsync(long followerId, long followingId, CancellationToken cancellationToken = default)
    {
        var alreadyFollowing = await IsFollowingAsync(followerId, followingId, cancellationToken);
        if (alreadyFollowing)
        {
            return;
        }

        _dbContext.Follows.Add(new Follow
        {
            FollowerId = followerId,
            FollowingId = followingId
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UnfollowAsync(long followerId, long followingId, CancellationToken cancellationToken = default)
    {
        var follow = await _dbContext.Follows
            .FirstOrDefaultAsync(follow => follow.FollowerId == followerId && follow.FollowingId == followingId, cancellationToken);
        if (follow is null)
        {
            return;
        }

        _dbContext.Follows.Remove(follow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
