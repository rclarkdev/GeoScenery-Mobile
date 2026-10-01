using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace GeoScenery.Api.Middleware;

/// <summary>
/// Converts otherwise-unhandled API exceptions into a generic Problem Details response.
/// The outer request logger records one database row for the resulting failed request.
/// </summary>
public sealed class ApiExceptionHandlingMiddleware(
    RequestDelegate next)
{
    public const string ExceptionTypeItemKey = "GeoScenery.ExceptionType";
    public const string ExceptionStackTraceItemKey = "GeoScenery.ExceptionStackTrace";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Items[ExceptionTypeItemKey] = nameof(OperationCanceledException);
            context.Response.StatusCode = 499;
            throw;
        }
        catch (Exception exception)
        {
            context.Items[ExceptionTypeItemKey] = exception.GetType().Name;
            context.Items[ExceptionStackTraceItemKey] = exception.StackTrace;

            if (context.Response.HasStarted)
            {
                throw;
            }

            var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault();
            context.Response.Clear();
            if (!string.IsNullOrEmpty(correlationId))
            {
                context.Response.Headers["X-Correlation-ID"] = correlationId;
            }
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Type = "https://httpstatuses.com/500",
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
        }
    }
}
