using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public class Visit
{
    public long Id { get; set; }

    public long SceneId { get; set; }

    public Scene Scene { get; set; } = null!;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    public DateTimeOffset VisitedAt { get; set; } = DateTimeOffset.UtcNow;
}