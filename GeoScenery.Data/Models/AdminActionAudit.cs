using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

/// <summary>Append-only record of a privileged administrative action.</summary>
public class AdminActionAudit
{
    public long Id { get; set; }

    public long? ActorUserId { get; set; }

    [Required, MaxLength(200)]
    public string ActorDisplayName { get; set; } = string.Empty;

    [Required, MaxLength(320)]
    public string ActorEmail { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ActionType { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string TargetType { get; set; } = string.Empty;

    public long? TargetId { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [MaxLength(4000)]
    public string? BeforeStateJson { get; set; }

    [MaxLength(4000)]
    public string? AfterStateJson { get; set; }

    [MaxLength(64)]
    public string? CorrelationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}