using System.Diagnostics;
using System.Security.Claims;
using GeoScenery.Api.Logging;

namespace GeoScenery.Api.Middleware;

public sealed class SqlRequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<SqlRequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ISqlAuditLog auditLog)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId)
            || correlationId.Length > 64
            || correlationId.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.' and not ':'))
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
            var isHealthRequest = context.Request.Path.StartsWithSegments("/health");
            if (!isHealthRequest || requestException is not null || context.Response.StatusCode >= 400)
            {
                var userId = long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                    ? parsedUserId
                    : (long?)null;
                var wasCancelled = requestException is OperationCanceledException && context.RequestAborted.IsCancellationRequested;
                var loggedExceptionType = requestException?.GetType().Name
                    ?? context.Items[ApiExceptionHandlingMiddleware.ExceptionTypeItemKey]?.ToString();
                var operationOutcome = RequestAuditContext.GetOperationOutcome(context);
                var operationFailed = operationOutcome is "Failure" or "PartialFailure";
                var level = wasCancelled ? "Warning" : requestException is not null || context.Response.StatusCode >= 500
                    ? "Error"
                    : context.Response.StatusCode >= 400 || operationFailed ? "Warning" : "Information";
                try
                {
                    var properties = new Dictionary<string, object?>
                    {
                        ["durationMilliseconds"] = stopwatch.ElapsedMilliseconds,
                        ["exceptionType"] = loggedExceptionType,
                        ["exceptionStackTrace"] = context.Items[ApiExceptionHandlingMiddleware.ExceptionStackTraceItemKey]?.ToString(),
                        ["outcome"] = wasCancelled ? "Cancelled"
                            : requestException is not null || loggedExceptionType is not null || context.Response.StatusCode >= 500 ? "Failure"
                            : operationOutcome ?? (context.Response.StatusCode >= 400 ? "Rejected" : "Success"),
                        ["endpoint"] = context.GetEndpoint()?.DisplayName
                    };
                    if (RequestAuditContext.Get(context) is { } operationProperties)
                    {
                        foreach (var property in operationProperties)
                        {
                            properties[property.Key] = property.Value;
                        }
                    }

                    await auditLog.WriteAsync(
                        context,
                        level,
                        "HttpRequest",
                        wasCancelled ? "HTTP request was cancelled."
                            : requestException is not null || loggedExceptionType is not null || context.Response.StatusCode >= 500
                                ? "HTTP request failed."
                                : operationFailed ? "HTTP request completed with an operation failure."
                                : context.Response.StatusCode >= 400 ? "HTTP request was rejected."
                                : "HTTP request completed.",
                        userId,
                        properties);
                }
                catch (Exception loggingException)
                {
                    logger.LogError(loggingException, "Request logging failed for {RequestPath}.", context.Request.Path);
                }
            }
        }
    }
}