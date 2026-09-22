using System.Security.Claims;
using System.Text.Json;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.AspNetCore.Http;

namespace GeoScenery.Api.Logging;

public sealed class SqlAuditLog(
    MyProjectDbContext dbContext,
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
            var resolvedUserId = userId ?? GetUserId(httpContext);
            dbContext.AppLogEntries.Add(new AppLogEntry
            {
                Level = level,
                EventName = eventName,
                Message = message,
                CorrelationId = GetCorrelationId(httpContext),
                HttpMethod = httpContext?.Request.Method,
                RequestPath = httpContext?.Request.Path.Value,
                StatusCode = httpContext?.Response.StatusCode,
                UserId = resolvedUserId,
                PropertiesJson = properties is null ? null : JsonSerializer.Serialize(properties)
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
        return httpContext?.Response.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? httpContext?.TraceIdentifier
            ?? Guid.NewGuid().ToString("N");
    }
}