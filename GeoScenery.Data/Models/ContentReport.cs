using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public static class ContentReportTargets
{
    public const string Profile = "Profile";
    public const string Scene = "Scene";
}

public static class ContentReportStatuses
{
    public const string Pending = "Pending";
    public const string Reviewed = "Reviewed";
    public const string Dismissed = "Dismissed";
    public const string Actioned = "Actioned";

    public static readonly string[] All = { Pending, Reviewed, Dismissed, Actioned };
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

    [Required, MaxLength(20)]
    public string Status { get; set; } = ContentReportStatuses.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedAt { get; set; }

    [MaxLength(1000)]
    public string? ResolutionNotes { get; set; }

    [MaxLength(50)]
    public string? ActionTaken { get; set; }

    public User? Reporter { get; set; }
}