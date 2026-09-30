using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Data.Services;

public sealed class UserBlockService(MyProjectDbContext dbContext) : IUserBlockService
{
    public Task<bool> IsBlockedAsync(long blockerId, long blockedId, CancellationToken cancellationToken = default)
    {
        return dbContext.UserBlocks
            .AsNoTracking()
            .AnyAsync(block => block.BlockerId == blockerId && block.BlockedId == blockedId, cancellationToken);
    }

    public Task<bool> HasBlockBetweenAsync(long firstUserId, long secondUserId, CancellationToken cancellationToken = default)
    {
        return dbContext.UserBlocks
            .AsNoTracking()
            .AnyAsync(block =>
                (block.BlockerId == firstUserId && block.BlockedId == secondUserId)
                || (block.BlockerId == secondUserId && block.BlockedId == firstUserId), cancellationToken);
    }

    public async Task BlockAsync(long blockerId, long blockedId, CancellationToken cancellationToken = default)
    {
        if (!await IsBlockedAsync(blockerId, blockedId, cancellationToken))
        {
            dbContext.UserBlocks.Add(new UserBlock { BlockerId = blockerId, BlockedId = blockedId });
        }

        var follows = await dbContext.Follows
            .Where(follow =>
                (follow.FollowerId == blockerId && follow.FollowingId == blockedId)
                || (follow.FollowerId == blockedId && follow.FollowingId == blockerId))
            .ToListAsync(cancellationToken);
        dbContext.Follows.RemoveRange(follows);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintGuard.IsUniqueConstraintViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    public async Task UnblockAsync(long blockerId, long blockedId, CancellationToken cancellationToken = default)
    {
        var block = await dbContext.UserBlocks
            .FirstOrDefaultAsync(candidate => candidate.BlockerId == blockerId && candidate.BlockedId == blockedId, cancellationToken);
        if (block is null)
        {
            return;
        }

        dbContext.UserBlocks.Remove(block);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}