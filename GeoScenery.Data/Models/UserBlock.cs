namespace GeoScenery.Data.Models;

public class UserBlock
{
    public long Id { get; set; }

    public long BlockerId { get; set; }

    public User Blocker { get; set; } = null!;

    public long BlockedId { get; set; }

    public User Blocked { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}