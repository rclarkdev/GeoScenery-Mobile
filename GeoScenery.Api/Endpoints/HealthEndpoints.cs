using GeoScenery.Data.Context;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => TypedResults.Ok(new HealthResponse("alive")))
            .AllowAnonymous()
            .WithName("Liveness");

        endpoints.MapGet("/health/ready", async Task<Results<Ok<HealthResponse>, ProblemHttpResult>>
            (MyProjectDbContext db, CancellationToken cancellationToken) =>
        {
            try
            {
                return await db.Database.CanConnectAsync(cancellationToken)
                    ? TypedResults.Ok(new HealthResponse("ready"))
                    : TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database is unavailable.");
            }
            catch (Exception)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database is unavailable.");
            }
        })
            .AllowAnonymous()
            .WithName("Readiness");

        return endpoints;
    }

    public sealed record HealthResponse(string Status);
}