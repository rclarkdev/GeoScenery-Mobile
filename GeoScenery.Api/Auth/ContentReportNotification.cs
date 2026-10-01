namespace GeoScenery.Api.Auth;

public sealed record ContentReportNotification(
    long ReportId,
    string TargetType,
    long TargetId,
    string TargetLabel,
    string ReporterDisplayName,
    string ReporterEmail,
    string Description,
    DateTimeOffset CreatedAt);