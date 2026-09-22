using GeoScenery.Data.Models;

namespace GeoScenery.Data.Services;

public interface IFollowService
{
    Task<bool> IsFollowingAsync(long followerId, long followingId, CancellationToken cancellationToken = default);
    Task<int> GetFollowerCountAsync(long userId, CancellationToken cancellationToken = default);
    Task<int> GetFollowingCountAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetFollowersAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetFollowingAsync(long userId, CancellationToken cancellationToken = default);
    Task FollowAsync(long followerId, long followingId, CancellationToken cancellationToken = default);
    Task UnfollowAsync(long followerId, long followingId, CancellationToken cancellationToken = default);
}
