using GeoScenery.Api.ViewModels;
using GeoScenery.Data.Models;
using GeoScenery.Data.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GeoScenery.Api.Endpoints;

public static class GeoSceneryEndpoints
{
    public static IEndpointRouteBuilder MapGeoSceneryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        MapUserEndpoints(api.MapGroup("/users"));
        MapSceneEndpoints(api.MapGroup("/scenes"));
        MapVisitEndpoints(api.MapGroup("/visits"));
        return endpoints;
    }

    private static void MapUserEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", async (IUserService service, CancellationToken cancellationToken) =>
            TypedResults.Ok((await service.GetAllAsync(cancellationToken)).Select(ToResponse)))
            .WithName("GetUsers");

        group.MapGet("/{id:long}", async Task<Results<Ok<UserResponse>, NotFound>>
            (long id, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.GetByIdAsync(id, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(user));
        })
        .WithName("GetUser");

        group.MapPost("", async Task<Created<UserResponse>>
            (CreateUserRequest request, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.CreateAsync(new User
            {
                DisplayName = request.DisplayName,
                Email = request.Email
            }, cancellationToken);
            return TypedResults.Created($"/api/users/{user.Id}", ToResponse(user));
        })
        .WithName("CreateUser");

        group.MapPut("/{id:long}", async Task<Results<Ok<UserResponse>, NotFound>>
            (long id, UpdateUserRequest request, IUserService service, CancellationToken cancellationToken) =>
        {
            var user = await service.UpdateAsync(id, new User
            {
                DisplayName = request.DisplayName,
                Email = request.Email
            }, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(user));
        });

        group.MapDelete("/{id:long}", async Task<Results<NoContent, NotFound>>
            (long id, IUserService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound());
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
            (CreateSceneRequest request, ISceneService service, CancellationToken cancellationToken) =>
        {
            var scene = await service.CreateAsync(new Scene
            {
                Title = request.Title,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                Rating = request.Rating,
                OwnerUserId = request.OwnerUserId
            }, cancellationToken);
            return TypedResults.Created($"/api/scenes/{scene.Id}", ToResponse(scene));
        })
        .WithName("CreateScene");

        group.MapPut("/{id:long}", async Task<Results<Ok<SceneResponse>, NotFound>>
            (long id, UpdateSceneRequest request, ISceneService service, CancellationToken cancellationToken) =>
        {
            var scene = await service.UpdateAsync(id, new Scene
            {
                Title = request.Title,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                Rating = request.Rating,
                OwnerUserId = request.OwnerUserId
            }, cancellationToken);
            return scene is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(scene));
        });

        group.MapDelete("/{id:long}", async Task<Results<NoContent, NotFound>>
            (long id, ISceneService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound());
    }

    private static void MapVisitEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("", async (IVisitService service, CancellationToken cancellationToken) =>
            TypedResults.Ok((await service.GetAllAsync(cancellationToken)).Select(ToResponse)))
            .WithName("GetVisits");

        group.MapGet("/user/{userId:long}", async (long userId, IVisitService service, CancellationToken cancellationToken) =>
            TypedResults.Ok((await service.GetByUserIdAsync(userId, cancellationToken)).Select(ToResponse)))
            .WithName("GetUserVisits");

        group.MapGet("/{id:long}", async Task<Results<Ok<VisitResponse>, NotFound>>
            (long id, IVisitService service, CancellationToken cancellationToken) =>
        {
            var visit = await service.GetByIdAsync(id, cancellationToken);
            return visit is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(visit));
        })
        .WithName("GetVisit");

        group.MapPost("", async Task<Created<VisitResponse>>
            (CreateVisitRequest request, IVisitService service, CancellationToken cancellationToken) =>
        {
            var visit = await service.CreateAsync(new Visit
            {
                SceneId = request.SceneId,
                UserId = request.UserId,
                VisitedAt = request.VisitedAt ?? DateTimeOffset.UtcNow
            }, cancellationToken);
            return TypedResults.Created($"/api/visits/{visit.Id}", ToResponse(visit));
        })
        .WithName("CreateVisit");

        group.MapPut("/{id:long}", async Task<Results<Ok<VisitResponse>, NotFound>>
            (long id, UpdateVisitRequest request, IVisitService service, CancellationToken cancellationToken) =>
        {
            var visit = await service.UpdateAsync(id, new Visit
            {
                SceneId = request.SceneId,
                UserId = request.UserId,
                VisitedAt = request.VisitedAt
            }, cancellationToken);
            return visit is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(visit));
        });

        group.MapDelete("/{id:long}", async Task<Results<NoContent, NotFound>>
            (long id, IVisitService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound());
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.DisplayName, user.Email, user.CreatedAt);

    private static SceneResponse ToResponse(Scene scene) =>
        new(scene.Id, scene.Title, scene.Description, scene.ImageUrl, scene.Rating, scene.OwnerUserId, scene.CreatedAt, scene.UpdatedAt);

    private static VisitResponse ToResponse(Visit visit) =>
        new(visit.Id, visit.SceneId, visit.UserId, visit.VisitedAt);
}