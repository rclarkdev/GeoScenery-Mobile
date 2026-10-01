using System.Security.Claims;
using System.Text.Json;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.AspNetCore.Http;

namespace GeoScenery.Api.Logging;

public sealed class SqlAuditLog(
    IServiceScopeFactory scopeFactory,
    ILogger<SqlAuditLog> logger) : ISqlAuditLog
{
    public async Task WriteAsync(
        HttpContext? httpContext,
        string level,
        string eventName,
        string message,
        long? userId = null,
        IReadOnlyDictionary<string, object?>? properties = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Persist through a clean, separate context so failed endpoint writes or
            // tracked entities cannot make the audit write commit/replay business data.
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
            var resolvedUserId = userId ?? GetUserId(httpContext);
            var durationMilliseconds = properties is not null
                && properties.TryGetValue("durationMilliseconds", out var duration)
                && duration is long elapsedMilliseconds
                    ? elapsedMilliseconds
                    : (long?)null;
            var serializedProperties = properties is null ? null : JsonSerializer.Serialize(SanitizeProperties(properties));
            if (serializedProperties?.Length > 4000)
            {
                serializedProperties = JsonSerializer.Serialize(new
                {
                    truncated = true,
                    propertyCount = properties!.Count
                });
            }

            dbContext.AppLogEntries.Add(new AppLogEntry
            {
                Level = level,
                EventName = eventName,
                Message = message,
                CorrelationId = GetCorrelationId(httpContext),
                HttpMethod = Limit(httpContext?.Request.Method, 10),
                RequestPath = Limit(httpContext?.Request.Path.Value, 512),
                StatusCode = httpContext?.Response.StatusCode,
                UserId = resolvedUserId,
                DurationMilliseconds = durationMilliseconds,
                PropertiesJson = serializedProperties
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to persist SQL audit log entry {EventName}.", eventName);
        }
    }

    private static long? GetUserId(HttpContext? httpContext)
    {
        var value = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static string GetCorrelationId(HttpContext? httpContext)
    {
        return Limit(httpContext?.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? httpContext?.TraceIdentifier
            ?? Guid.NewGuid().ToString("N"), 64)!;
    }

    private static string? Limit(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength ? value : value[..maximumLength];

    private static IReadOnlyDictionary<string, object?> SanitizeProperties(IReadOnlyDictionary<string, object?> properties)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in properties.Take(32))
        {
            var safeKey = Limit(key, 100) ?? string.Empty;
            var normalizedKey = safeKey.Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            var containsSecret = normalizedKey.Contains("password", StringComparison.Ordinal)
                || normalizedKey.Contains("token", StringComparison.Ordinal)
                || normalizedKey.Contains("secret", StringComparison.Ordinal)
                || normalizedKey.Contains("credential", StringComparison.Ordinal)
                || normalizedKey.Contains("authorization", StringComparison.Ordinal)
                || normalizedKey.EndsWith("url", StringComparison.Ordinal)
                || normalizedKey.Contains("emailaddress", StringComparison.Ordinal);
            sanitized[safeKey] = containsSecret
                ? "[REDACTED]"
                : value is string text
                    ? Limit(text, normalizedKey.Contains("stacktrace", StringComparison.Ordinal) ? 2000 : 512)
                    : value;
        }

        return sanitized;
    }
}