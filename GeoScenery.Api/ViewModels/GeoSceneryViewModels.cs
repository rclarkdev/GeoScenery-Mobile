using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Api.ViewModels;

/// <summary>Represents a scene returned by the API.</summary>
public sealed record SceneResponse(
    long Id,
    string Title,
    string Description,
    string ImageUrl,
    decimal Rating,
    long? OwnerUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Payload for creating a scene.</summary>
public sealed record CreateSceneRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(4000)]
    public required string Description { get; init; }

    [Required, MaxLength(2048), Url]
    public required string ImageUrl { get; init; }

    [Range(0, 10)]
    public decimal Rating { get; init; }

}

/// <summary>Payload for updating a scene.</summary>
public sealed record UpdateSceneRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(4000)]
    public required string Description { get; init; }

    [Required, MaxLength(2048), Url]
    public required string ImageUrl { get; init; }

    [Range(0, 10)]
    public decimal Rating { get; init; }

}

/// <summary>Represents a user returned by the API.</summary>
public sealed record UserResponse(long Id, string DisplayName, string Email, DateTimeOffset CreatedAt);

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