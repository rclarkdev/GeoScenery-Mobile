using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public class User
{
    public long Id { get; set; }

    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Scene> Scenes { get; set; } = new List<Scene>();

    public ICollection<Visit> Visits { get; set; } = new List<Visit>();
}
