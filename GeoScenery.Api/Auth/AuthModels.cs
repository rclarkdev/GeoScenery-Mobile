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

    [Required, Compare(nameof(Password))]
    public required string ConfirmPassword { get; init; }
}

public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public sealed record AuthResponse(long UserId, string DisplayName, string Email, string Token);

public sealed record RegistrationResponse(string Message, bool EmailSent);

public sealed record EmailVerificationResponse(string Message, bool? EmailSent);

public sealed record ResendVerificationRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }
}

public sealed record VerifyEmailRequest
{
    [Required]
    public required string Token { get; init; }
}

public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }
}

public sealed record ResetPasswordRequest
{
    [Required]
    public required string Token { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string Password { get; init; }
}

public sealed record PasswordResetResponse(string Message);