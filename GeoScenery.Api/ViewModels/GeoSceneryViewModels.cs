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
    bool IsBlockedByCurrentUser,
    bool HasBlockedCurrentUser,
    DateTimeOffset CreatedAt,
    bool CanMessage);

/// <summary>Represents a message exchanged between two users.</summary>
public sealed record MessageResponse(
    long Id,
    long SenderId,
    long RecipientId,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>Represents the latest message in a user's inbox.</summary>
public sealed record ConversationResponse(
    UserSummaryResponse User,
    MessageResponse LastMessage,
    int UnreadCount);

/// <summary>Payload for sending a message to another user.</summary>
public sealed record SendMessageRequest
{
    [Required, MaxLength(2000)]
    public required string Body { get; init; }
}

/// <summary>Represents a user in a follower/following list.</summary>
public sealed record UserSummaryResponse(long Id, string DisplayName, string? ProfileImageUrl);

/// <summary>Administrative view of a user's account and assigned application roles.</summary>
public sealed record AdminUserResponse(long Id, string DisplayName, string Email, IReadOnlyList<string> Roles);

/// <summary>Report information visible to administrators.</summary>
public sealed record AdminContentReportResponse(
    long Id,
    string TargetType,
    long TargetId,
    string TargetLabel,
    string ReporterDisplayName,
    string ReporterEmail,
    string Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNotes,
    string? ActionTaken);

/// <summary>Updates the moderation status and optional resolution note.</summary>
public sealed record UpdateContentReportStatusRequest
{
    [Required, MaxLength(20)]
    public required string Status { get; init; }

    [MaxLength(1000)]
    public string? ResolutionNotes { get; init; }

    [MaxLength(50)]
    public string? ActionTaken { get; init; }
}

/// <summary>Replaces the roles assigned to a user.</summary>
public sealed record UpdateUserRolesRequest
{
    [Required, MinLength(1)]
    public required IReadOnlyList<string> Roles { get; init; }
}

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

/// <summary>Payload for securely changing the authenticated user's email.</summary>
public sealed record ChangeEmailRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [Required]
    public required string CurrentPassword { get; init; }
}

/// <summary>Payload for securely changing the authenticated user's password.</summary>
public sealed record ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>Represents a recorded scene visit.</summary>
public sealed record VisitResponse(long Id, long SceneId, long UserId, string? SceneTitle, DateTimeOffset VisitedAt);

/// <summary>Response with the server-hosted URL of a successfully uploaded image.</summary>
public sealed record UploadedImageResponse(string Url);

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

/// <summary>Payload submitted when reporting a profile or scene.</summary>
public sealed record CreateContentReportRequest
{
    [Required, MaxLength(2000)]
    public required string Description { get; init; }
}

/// <summary>Confirmation returned after a content report has been recorded.</summary>
public sealed record ContentReportResponse(long Id, string TargetType, long TargetId, DateTimeOffset CreatedAt);