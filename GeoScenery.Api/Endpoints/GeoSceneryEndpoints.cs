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
            (ClaimsPrincipal principal, IUserService service, IFollowService followService, CancellationToken cancellationToken) =>
        {
            var viewerId = GetUserId(principal);
            var user = await service.GetByIdAsync(viewerId, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(await ToResponseAsync(user, viewerId, followService, cancellationToken));
        })
        .WithName("GetCurrentUser");

        group.MapGet("/{id:long}", async Task<Results<Ok<UserResponse>, NotFound>>
            (long id, ClaimsPrincipal principal, IUserService service, IFollowService followService, CancellationToken cancellationToken) =>
        {
            var user = await service.GetByIdAsync(id, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(await ToResponseAsync(user, GetUserId(principal), followService, cancellationToken));
        })
        .WithName("GetUser");

        group.MapPut("/{id:long}", async Task<Results<Ok<UserResponse>, NotFound>>
            (long id, UpdateUserRequest request, ClaimsPrincipal principal, IUserService service, IFollowService followService, CancellationToken cancellationToken) =>
        {
            if (id != GetUserId(principal))
            {
                return TypedResults.NotFound();
            }

            var user = await service.UpdateAsync(id, new User
            {
                DisplayName = request.DisplayName,
                Email = request.Email,
                ProfileImageUrl = request.ProfileImageUrl,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            }, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(await ToResponseAsync(user, id, followService, cancellationToken));
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

        group.MapPost("/{id:long}/follow", async Task<Results<NoContent, NotFound, BadRequest>>
            (long id, ClaimsPrincipal principal, IUserService userService, IFollowService followService, CancellationToken cancellationToken) =>
        {
            var followerId = GetUserId(principal);
            if (followerId == id)
            {
                return TypedResults.BadRequest();
            }

            var target = await userService.GetByIdAsync(id, cancellationToken);
            if (target is null)
            {
                return TypedResults.NotFound();
            }

            await followService.FollowAsync(followerId, id, cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("FollowUser");

        group.MapDelete("/{id:long}/follow", async Task<NoContent>
            (long id, ClaimsPrincipal principal, IFollowService followService, CancellationToken cancellationToken) =>
        {
            await followService.UnfollowAsync(GetUserId(principal), id, cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("UnfollowUser");

        group.MapGet("/{id:long}/followers", async Task<Results<Ok<IEnumerable<UserSummaryResponse>>, NotFound>>
            (long id, IUserService userService, IFollowService followService, CancellationToken cancellationToken) =>
        {
            var target = await userService.GetByIdAsync(id, cancellationToken);
            if (target is null)
            {
                return TypedResults.NotFound();
            }

            var followers = await followService.GetFollowersAsync(id, cancellationToken);
            return TypedResults.Ok(followers.Select(ToSummaryResponse));
        })
        .WithName("GetUserFollowers");

        group.MapGet("/{id:long}/following", async Task<Results<Ok<IEnumerable<UserSummaryResponse>>, NotFound>>
            (long id, IUserService userService, IFollowService followService, CancellationToken cancellationToken) =>
        {
            var target = await userService.GetByIdAsync(id, cancellationToken);
            if (target is null)
            {
                return TypedResults.NotFound();
            }

            var following = await followService.GetFollowingAsync(id, cancellationToken);
            return TypedResults.Ok(following.Select(ToSummaryResponse));
        })
        .WithName("GetUserFollowing");
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
                Latitude = request.Latitude,
                Longitude = request.Longitude,
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
                Latitude = request.Latitude,
                Longitude = request.Longitude,
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

    private static async Task<UserResponse> ToResponseAsync(User user, long viewerId, IFollowService followService, CancellationToken cancellationToken)
    {
        var followerCount = await followService.GetFollowerCountAsync(user.Id, cancellationToken);
        var followingCount = await followService.GetFollowingCountAsync(user.Id, cancellationToken);
        var isFollowedByCurrentUser = viewerId != user.Id && await followService.IsFollowingAsync(viewerId, user.Id, cancellationToken);
        var email = user.Id == viewerId ? user.Email : null;
        return new(user.Id, user.DisplayName, email, user.ProfileImageUrl, user.Latitude, user.Longitude, followerCount, followingCount, isFollowedByCurrentUser, user.CreatedAt);
    }

    private static UserSummaryResponse ToSummaryResponse(User user) =>
        new(user.Id, user.DisplayName, user.ProfileImageUrl);

    private static SceneResponse ToResponse(Scene scene) =>
        new(scene.Id, scene.Title, scene.Description, scene.ImageUrl, scene.Rating, scene.Latitude, scene.Longitude, scene.OwnerUserId, scene.CreatedAt, scene.UpdatedAt);

    private static VisitResponse ToResponse(Visit visit) =>
        new(visit.Id, visit.SceneId, visit.UserId, visit.Scene?.Title, visit.VisitedAt);

    private static long GetUserId(ClaimsPrincipal principal) =>
        long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user id is missing."));
}