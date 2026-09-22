namespace GeoScenery.Data.Models;

public class Follow
{
    public long Id { get; set; }

    public long FollowerId { get; set; }

    public User Follower { get; set; } = null!;

    public long FollowingId { get; set; }

    public User Following { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
