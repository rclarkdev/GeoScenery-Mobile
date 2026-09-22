using GeoScenery.Api.ViewModels;
using GeoScenery.Data.Models;
using GeoScenery.Data.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;

namespace GeoScenery.Api.Endpoints;

public static class GeoSceneryEndpoints
{
    public static IEndpointRouteBuilder MapGeoSceneryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        MapUserEndpoints(api.MapGroup("/users"));
        MapSceneEndpoints(api.MapGroup("/scenes"));
        MapVisitEndpoints(api.MapGroup("/visits").RequireAuthorization());
        return endpoints;
    }

    private static void MapUserEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapGet("/me", async Task<Results<Ok<UserResponse>, NotFound>>
            (ClaimsPrincipal principal, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.GetByIdAsync(GetUserId(principal), cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(user));
        })
        .WithName("GetCurrentUser");

        group.MapGet("/{id:long}", async Task<Results<Ok<UserResponse>, NotFound>>
            (long id, ClaimsPrincipal principal, IUserService service, CancellationToken cancellationToken) =>
        {
            if (id != GetUserId(principal))
            {
                return TypedResults.NotFound();
            }

            var user = await service.GetByIdAsync(id, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(user));
        })
        .WithName("GetUser");

        group.MapPut("/{id:long}", async Task<Results<Ok<UserResponse>, NotFound>>
            (long id, UpdateUserRequest request, ClaimsPrincipal principal, IUserService service, CancellationToken cancellationToken) =>
        {
            if (id != GetUserId(principal))
            {
                return TypedResults.NotFound();
            }

            var user = await service.UpdateAsync(id, new User
            {
                DisplayName = request.DisplayName,
                Email = request.Email
            }, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(user));
        });

        group.MapDelete("/{id:long}", async Task<Results<NoContent, NotFound>>
            (long id, ClaimsPrincipal principal, IUserService service, CancellationToken cancellationToken) =>
        {
            if (id != GetUserId(principal))
            {
                return TypedResults.NotFound();
            }

            return await service.DeleteAsync(id, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound();
        });
    }

    private static void MapSceneEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", async (ISceneService service, CancellationToken cancellationToken) =>
            TypedResults.Ok((await service.GetAllAsync(cancellationToken)).Select(ToResponse)))
            .WithName("GetScenes");

        group.MapGet("/{id:long}", async Task<Results<Ok<SceneResponse>, NotFound>>
            (long id, ISceneService service, CancellationToken cancellationToken) =>
        {
            var scene = await service.GetByIdAsync(id, cancellationToken);
            return scene is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(scene));
        })
        .WithName("GetScene");

        group.MapPost("", async Task<Created<SceneResponse>>
            (CreateSceneRequest request, ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
        {
            var scene = await service.CreateAsync(new Scene
            {
                Title = request.Title,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                Rating = request.Rating,
                OwnerUserId = GetUserId(principal)
            }, cancellationToken);
            return TypedResults.Created($"/api/scenes/{scene.Id}", ToResponse(scene));
        })
        .WithName("CreateScene")
        .RequireAuthorization();

        group.MapPut("/{id:long}", async Task<Results<Ok<SceneResponse>, NotFound>>
            (long id, UpdateSceneRequest request, ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
        {
            var scene = await service.UpdateAsync(id, new Scene
            {
                Title = request.Title,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                Rating = request.Rating,
                OwnerUserId = GetUserId(principal)
            }, GetUserId(principal), cancellationToken);
            return scene is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(scene));
        })
        .RequireAuthorization();

        group.MapDelete("/{id:long}", async Task<Results<NoContent, NotFound>>
            (long id, ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, GetUserId(principal), cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound())
            .RequireAuthorization();
    }

    private static void MapVisitEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/me", async (ClaimsPrincipal principal, IVisitService service, CancellationToken cancellationToken) =>
            TypedResults.Ok((await service.GetByUserIdAsync(GetUserId(principal), cancellationToken)).Select(ToResponse)))
            .WithName("GetUserVisits");

        group.MapGet("/{id:long}", async Task<Results<Ok<VisitResponse>, NotFound>>
            (long id, ClaimsPrincipal principal, IVisitService service, CancellationToken cancellationToken) =>
        {
            var visit = await service.GetByIdAsync(id, GetUserId(principal), cancellationToken);
            return visit is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(visit));
        })
        .WithName("GetVisit");

        group.MapPost("", async Task<Created<VisitResponse>>
            (CreateVisitRequest request, ClaimsPrincipal principal, IVisitService service, CancellationToken cancellationToken) =>
        {
            var visit = await service.CreateAsync(new Visit
            {
                SceneId = request.SceneId,
                UserId = GetUserId(principal),
                VisitedAt = request.VisitedAt ?? DateTimeOffset.UtcNow
            }, cancellationToken);
            return TypedResults.Created($"/api/visits/{visit.Id}", ToResponse(visit));
        })
        .WithName("CreateVisit");

        group.MapPut("/{id:long}", async Task<Results<Ok<VisitResponse>, NotFound>>
            (long id, UpdateVisitRequest request, ClaimsPrincipal principal, IVisitService service, CancellationToken cancellationToken) =>
        {
            var visit = await service.UpdateAsync(id, new Visit
            {
                SceneId = request.SceneId,
                UserId = GetUserId(principal),
                VisitedAt = request.VisitedAt
            }, GetUserId(principal), cancellationToken);
            return visit is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(visit));
        });

        group.MapDelete("/{id:long}", async Task<Results<NoContent, NotFound>>
            (long id, ClaimsPrincipal principal, IVisitService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, GetUserId(principal), cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound())
            .RequireAuthorization();
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.DisplayName, user.Email, user.CreatedAt);

    private static SceneResponse ToResponse(Scene scene) =>
        new(scene.Id, scene.Title, scene.Description, scene.ImageUrl, scene.Rating, scene.OwnerUserId, scene.CreatedAt, scene.UpdatedAt);

    private static VisitResponse ToResponse(Visit visit) =>
        new(visit.Id, visit.SceneId, visit.UserId, visit.Scene?.Title, visit.VisitedAt);

    private static long GetUserId(ClaimsPrincipal principal) =>
        long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user id is missing."));
}