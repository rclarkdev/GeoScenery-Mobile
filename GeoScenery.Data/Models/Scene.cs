using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public class Scene
{
    public long Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(2048)]
    public string ImageUrl { get; set; } = string.Empty;

    [Range(0, 10)]
    public decimal Rating { get; set; }

    public long? OwnerUserId { get; set; }

    public User? OwnerUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Visit> Visits { get; set; } = new List<Visit>();
}