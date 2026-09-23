using GeoScenery.Api.ViewModels;
using GeoScenery.Api.Storage;
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

        // The authenticated user's own scenes. The owner id is always derived from
        // the token; there is intentionally no {userId}/scenes route so another
        // user's private scene list cannot be requested by parameter manipulation.
        group.MapGet("/me/scenes", async Task<Ok<List<SceneResponse>>>
            (ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
        {
            var viewerId = GetUserId(principal);
            var scenes = await service.GetByOwnerAsync(viewerId, cancellationToken);
            return TypedResults.Ok(scenes.Select(scene => ToResponse(scene, viewerId: viewerId)).ToList());
        })
        .WithName("GetMyScenes");

        group.MapGet("/{id:long}", async Task<Results<Ok<UserResponse>, NotFound>>
            (long id, ClaimsPrincipal principal, IUserService service, IFollowService followService, CancellationToken cancellationToken) =>
        {
            var user = await service.GetByIdAsync(id, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(await ToResponseAsync(user, GetUserId(principal), followService, cancellationToken));
        })
        .WithName("GetUser");

        group.MapPut("/{id:long}", async Task<Results<Ok<UserResponse>, NotFound, Conflict<string>, BadRequest<string>>>
            (long id, UpdateUserRequest request, ClaimsPrincipal principal, IUserService service, IFollowService followService, IFileStorageService storage, CancellationToken cancellationToken) =>
        {
            if (id != GetUserId(principal))
            {
                return TypedResults.NotFound();
            }

            // A profile image must be a reference to an image uploaded through the
            // authenticated upload endpoint; arbitrary client-supplied URLs or
            // base64 data are rejected (unless cleared with a null/empty value).
            if (!string.IsNullOrEmpty(request.ProfileImageUrl)
                && !await storage.IsStoredImageAsync(request.ProfileImageUrl, cancellationToken))
            {
                return TypedResults.BadRequest("profileImageUrl must reference a previously uploaded image.");
            }

            try
            {
                var user = await service.UpdateAsync(id, new User
                {
                    DisplayName = request.DisplayName,
                    Email = request.Email,
                    ProfileImageUrl = string.IsNullOrEmpty(request.ProfileImageUrl) ? null : request.ProfileImageUrl,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    BirthDate = request.BirthDate,
                    Education = request.Education,
                    Hobbies = request.Hobbies,
                    Employment = request.Employment,
                    Bio = request.Bio
                }, cancellationToken);
                return user is null ? TypedResults.NotFound() : TypedResults.Ok(await ToResponseAsync(user, id, followService, cancellationToken));
            }
            catch (DuplicateEmailException)
            {
                return TypedResults.Conflict("An account with this email already exists.");
            }
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
        group.MapGet("", async (string? tags, double? latitude, double? longitude, double? radiusKm, ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
        {
            var tagList = string.IsNullOrWhiteSpace(tags)
                ? null
                : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var viewerId = TryGetUserId(principal);
            var results = await service.SearchAsync(tagList, latitude, longitude, radiusKm, cancellationToken);
            return TypedResults.Ok(results.Select(result => ToResponse(result.Scene, result.DistanceKm, viewerId)));
        })
        .WithName("GetScenes");

        group.MapGet("/{id:long}", async Task<Results<Ok<SceneResponse>, NotFound>>
            (long id, ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
        {
            var scene = await service.GetByIdAsync(id, cancellationToken);
            return scene is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(scene, viewerId: TryGetUserId(principal)));
        })
        .WithName("GetScene");

        group.MapPost("", async Task<Results<Created<SceneResponse>, BadRequest<string>>>
            (CreateSceneRequest request, ClaimsPrincipal principal, ISceneService service, IFileStorageService storage, CancellationToken cancellationToken) =>
        {
            // Only references to images uploaded through the authenticated upload
            // endpoint are accepted; arbitrary client-supplied URLs and encoded
            // image data are rejected.
            if (!await storage.IsStoredImageAsync(request.ImageUrl, cancellationToken))
            {
                return TypedResults.BadRequest("imageUrl must reference a previously uploaded image.");
            }

            var tagError = ValidateTags(request.Tags);
            if (tagError is not null)
            {
                return TypedResults.BadRequest(tagError);
            }

            var scene = await service.CreateAsync(new Scene
            {
                Title = request.Title,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                Rating = request.Rating,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                OwnerUserId = GetUserId(principal)
            }, request.Tags, cancellationToken);
            return TypedResults.Created($"/api/scenes/{scene.Id}", ToResponse(scene, viewerId: GetUserId(principal)));
        })
        .WithName("CreateScene")
        .RequireAuthorization();

        group.MapPut("/{id:long}", async Task<Results<Ok<SceneResponse>, NotFound, BadRequest<string>>>
            (long id, UpdateSceneRequest request, ClaimsPrincipal principal, ISceneService service, IFileStorageService storage, CancellationToken cancellationToken) =>
        {
            var tagError = ValidateTags(request.Tags);
            if (tagError is not null)
            {
                return TypedResults.BadRequest(tagError);
            }

            // Reject arbitrary image URLs/data; the submitted image must be a valid
            // reference produced by the upload endpoint (e.g. the unchanged value
            // loaded from the scene itself).
            if (!await storage.IsStoredImageAsync(request.ImageUrl, cancellationToken))
            {
                return TypedResults.BadRequest("imageUrl must reference a previously uploaded image.");
            }

            var scene = await service.UpdateAsync(id, new Scene
            {
                Title = request.Title,
                Description = request.Description,
                ImageUrl = request.ImageUrl,
                Rating = request.Rating,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                OwnerUserId = GetUserId(principal)
            }, GetUserId(principal), request.Tags, cancellationToken);
            return scene is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(scene, viewerId: GetUserId(principal)));
        })
        .RequireAuthorization();

        group.MapDelete("/{id:long}", async Task<Results<NoContent, NotFound>>
            (long id, ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, GetUserId(principal), cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound())
            .RequireAuthorization();

        group.MapPost("/{id:long}/rating", async Task<Results<Ok<SceneResponse>, NotFound, BadRequest>>
            (long id, RateSceneRequest request, ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(principal);
            var scene = await service.GetByIdAsync(id, cancellationToken);
            if (scene is null)
            {
                return TypedResults.NotFound();
            }

            if (scene.OwnerUserId == userId)
            {
                return TypedResults.BadRequest();
            }

            var rated = await service.RateAsync(id, userId, request.Rating, cancellationToken);
            return TypedResults.Ok(ToResponse(rated!, viewerId: userId));
        })
        .WithName("RateScene")
        .RequireAuthorization();

        group.MapDelete("/{id:long}/rating", async Task<Results<NoContent, NotFound>>
            (long id, ClaimsPrincipal principal, ISceneService service, CancellationToken cancellationToken) =>
            await service.RemoveRatingAsync(id, GetUserId(principal), cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound())
        .WithName("RemoveSceneRating")
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

        group.MapPost("", async Task<Results<Ok<VisitResponse>, Created<VisitResponse>>>
            (CreateVisitRequest request, ClaimsPrincipal principal, IVisitService service, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(principal);

            // A (user, scene) visit is unique: recording one again is idempotent
            // and returns the existing row with 200 instead of failing on the
            // database's unique index.
            var existing = await service.GetBySceneAsync(userId, request.SceneId, cancellationToken);
            if (existing is not null)
            {
                return TypedResults.Ok(ToResponse(existing));
            }

            var visit = await service.CreateAsync(new Visit
            {
                SceneId = request.SceneId,
                UserId = userId,
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
        return new(user.Id, user.DisplayName, email, user.ProfileImageUrl, user.Latitude, user.Longitude,
            user.BirthDate, user.Education, user.Hobbies, user.Employment, user.Bio,
            followerCount, followingCount, isFollowedByCurrentUser, user.CreatedAt);
    }

    private static UserSummaryResponse ToSummaryResponse(User user) =>
        new(user.Id, user.DisplayName, user.ProfileImageUrl);

    private static SceneResponse ToResponse(Scene scene, double? distanceKm = null, long? viewerId = null)
    {
        var ratingCount = scene.Ratings.Count;
        var averageRating = ratingCount > 0 ? (double?)scene.Ratings.Average(sceneRating => sceneRating.Rating) : null;
        var currentUserRating = viewerId.HasValue
            ? scene.Ratings.FirstOrDefault(sceneRating => sceneRating.UserId == viewerId.Value)?.Rating
            : null;
        return new(scene.Id, scene.Title, scene.Description, scene.ImageUrl, scene.Rating, scene.Latitude, scene.Longitude,
            scene.Tags.Select(tag => tag.Tag).OrderBy(tag => tag).ToList(), distanceKm,
            averageRating, ratingCount, currentUserRating,
            scene.OwnerUserId, scene.CreatedAt, scene.UpdatedAt);
    }

    private static VisitResponse ToResponse(Visit visit) =>
        new(visit.Id, visit.SceneId, visit.UserId, visit.Scene?.Title, visit.VisitedAt);

    private static long GetUserId(ClaimsPrincipal principal) =>
        long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user id is missing."));

    private static long? TryGetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return value is not null && long.TryParse(value, out var userId) ? userId : null;
    }

    private static string? ValidateTags(IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return null;
        }

        return tags.Any(tag => tag.Length > SceneTag.MaxTagLength)
            ? $"Each tag must be {SceneTag.MaxTagLength} characters or fewer."
            : null;
    }
}