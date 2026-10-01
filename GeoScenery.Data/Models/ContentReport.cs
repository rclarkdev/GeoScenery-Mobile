using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public static class ContentReportTargets
{
    public const string Profile = "Profile";
    public const string Scene = "Scene";
}

public class ContentReport
{
    public long Id { get; set; }

    public long? ReporterId { get; set; }

    [Required, MaxLength(200)]
    public string ReporterDisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(320)]
    public string ReporterEmail { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string TargetType { get; set; } = string.Empty;

    public long TargetId { get; set; }

    [Required, MaxLength(200)]
    public string TargetLabel { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? Reporter { get; set; }
}