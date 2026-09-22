using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public class AppLogEntry
{
    public long Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required, MaxLength(16)]
    public string Level { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string EventName { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(64)]
    public string CorrelationId { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? HttpMethod { get; set; }

    [MaxLength(512)]
    public string? RequestPath { get; set; }

    public int? StatusCode { get; set; }

    public long? UserId { get; set; }

    public long? DurationMilliseconds { get; set; }

    [MaxLength(4000)]
    public string? PropertiesJson { get; set; }
}