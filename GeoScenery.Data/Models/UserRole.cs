namespace GeoScenery.Data.Models;

public class UserRole
{
    public long UserId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}