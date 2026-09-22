using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Api.ViewModels;

/// <summary>Represents a scene returned by the API.</summary>
public sealed record SceneResponse(
    long Id,
    string Title,
    string Description,
    string ImageUrl,
    decimal Rating,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<string> Tags,
    double? DistanceKm,
    double? AverageRating,
    int RatingCount,
    decimal? CurrentUserRating,
    long? OwnerUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Payload for rating another user's scene.</summary>
public sealed record RateSceneRequest
{
    [Range(0, 10)]
    public decimal Rating { get; init; }
}

/// <summary>Payload for creating a scene.</summary>
public sealed record CreateSceneRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(4000)]
    public required string Description { get; init; }

    [Required]
    public required string ImageUrl { get; init; }

    [Range(0, 10)]
    public decimal Rating { get; init; }

    [Range(-90, 90)]
    public double? Latitude { get; init; }

    [Range(-180, 180)]
    public double? Longitude { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

}

/// <summary>Payload for updating a scene.</summary>
public sealed record UpdateSceneRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(4000)]
    public required string Description { get; init; }

    [Required]
    public required string ImageUrl { get; init; }

    [Range(0, 10)]
    public decimal Rating { get; init; }

    [Range(-90, 90)]
    public double? Latitude { get; init; }

    [Range(-180, 180)]
    public double? Longitude { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

}

/// <summary>Represents a user returned by the API. Email is only populated when viewing your own profile.</summary>
public sealed record UserResponse(
    long Id,
    string DisplayName,
    string? Email,
    string? ProfileImageUrl,
    double? Latitude,
    double? Longitude,
    DateOnly? BirthDate,
    string? Education,
    string? Hobbies,
    string? Employment,
    string? Bio,
    int FollowerCount,
    int FollowingCount,
    bool IsFollowedByCurrentUser,
    DateTimeOffset CreatedAt);

/// <summary>Represents a user in a follower/following list.</summary>
public sealed record UserSummaryResponse(long Id, string DisplayName, string? ProfileImageUrl);

/// <summary>Payload for creating a user.</summary>
public sealed record CreateUserRequest
{
    [Required, MaxLength(200)]
    public required string DisplayName { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }
}

/// <summary>Payload for updating a user.</summary>
public sealed record UpdateUserRequest
{
    [Required, MaxLength(200)]
    public required string DisplayName { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    public string? ProfileImageUrl { get; init; }

    [Range(-90, 90)]
    public double? Latitude { get; init; }

    [Range(-180, 180)]
    public double? Longitude { get; init; }

    public DateOnly? BirthDate { get; init; }

    [MaxLength(200)]
    public string? Education { get; init; }

    [MaxLength(500)]
    public string? Hobbies { get; init; }

    [MaxLength(200)]
    public string? Employment { get; init; }

    [MaxLength(2000)]
    public string? Bio { get; init; }
}

/// <summary>Represents a recorded scene visit.</summary>
public sealed record VisitResponse(long Id, long SceneId, long UserId, string? SceneTitle, DateTimeOffset VisitedAt);

/// <summary>Payload for recording a scene visit.</summary>
public sealed record CreateVisitRequest
{
    [Range(1, long.MaxValue)]
    public long SceneId { get; init; }

    public DateTimeOffset? VisitedAt { get; init; }
}

/// <summary>Payload for updating a recorded scene visit.</summary>
public sealed record UpdateVisitRequest
{
    [Range(1, long.MaxValue)]
    public long SceneId { get; init; }

    public DateTimeOffset VisitedAt { get; init; }
}