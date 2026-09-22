using System.Diagnostics;
using System.Security.Claims;
using GeoScenery.Api.Logging;

namespace GeoScenery.Api.Middleware;

public sealed class SqlRequestLoggingMiddleware(
    RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISqlAuditLog auditLog)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Response.Headers["X-Correlation-ID"] = correlationId;
        var stopwatch = Stopwatch.StartNew();
        Exception? requestException = null;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            requestException = exception;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (!context.Request.Path.StartsWithSegments("/health"))
            {
                var userId = long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                    ? parsedUserId
                    : (long?)null;
                var level = requestException is not null || context.Response.StatusCode >= 500
                    ? "Error"
                    : context.Response.StatusCode >= 400 ? "Warning" : "Information";
                await auditLog.WriteAsync(
                    context,
                    level,
                    "HttpRequest",
                    requestException is null ? "HTTP request completed." : "HTTP request failed.",
                    userId,
                    new Dictionary<string, object?>
                    {
                        ["durationMilliseconds"] = stopwatch.ElapsedMilliseconds,
                        ["exceptionType"] = requestException?.GetType().Name
                    });
            }
        }
    }
}