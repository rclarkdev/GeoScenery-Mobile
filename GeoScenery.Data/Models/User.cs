using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public class User
{
    public long Id { get; set; }

    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public string? ProfileImageUrl { get; set; }

    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    public DateOnly? BirthDate { get; set; }

    [MaxLength(200)]
    public string? Education { get; set; }

    [MaxLength(500)]
    public string? Hobbies { get; set; }

    [MaxLength(200)]
    public string? Employment { get; set; }

    [MaxLength(2000)]
    public string? Bio { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Scene> Scenes { get; set; } = new List<Scene>();

    public ICollection<Visit> Visits { get; set; } = new List<Visit>();

    /// <summary>Follow rows where this user is the follower (who they follow).</summary>
    public ICollection<Follow> Following { get; set; } = new List<Follow>();

    /// <summary>Follow rows where this user is being followed (their followers).</summary>
    public ICollection<Follow> Followers { get; set; } = new List<Follow>();
}
