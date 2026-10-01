using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public static class AppRoles
{
    public const string Member = "Member";
    public const string Admin = "Admin";
}

public class Role
{
    [Key, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}