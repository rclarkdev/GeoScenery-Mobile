using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public class SceneRating
{
    public long Id { get; set; }

    public long SceneId { get; set; }

    public Scene Scene { get; set; } = null!;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    [Range(0, 10)]
    public decimal Rating { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
