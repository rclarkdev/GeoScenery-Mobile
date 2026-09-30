namespace GeoScenery.Data.Services;

public interface IUserBlockService
{
    Task<bool> IsBlockedAsync(long blockerId, long blockedId, CancellationToken cancellationToken = default);
    Task<bool> HasBlockBetweenAsync(long firstUserId, long secondUserId, CancellationToken cancellationToken = default);
    Task BlockAsync(long blockerId, long blockedId, CancellationToken cancellationToken = default);
    Task UnblockAsync(long blockerId, long blockedId, CancellationToken cancellationToken = default);
}