using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Api.Auth;

public sealed record RegisterRequest
{
    [Required, MaxLength(200)]
    public required string DisplayName { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string Password { get; init; }
}

public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public sealed record AuthResponse(long UserId, string DisplayName, string Email, string Token);