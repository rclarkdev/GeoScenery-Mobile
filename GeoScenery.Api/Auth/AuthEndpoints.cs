using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using GeoScenery.Data.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GeoScenery.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/register", async Task<Results<Ok<RegistrationResponse>, Conflict<string>, BadRequest<string>>>
            (RegisterRequest request, MyProjectDbContext db, IPasswordHasher<User> hasher, IEmailSender emailSender,
                IConfiguration appConfiguration, IHostEnvironment environment, ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
        {
            if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            {
                return TypedResults.BadRequest("Passwords do not match.");
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
            {
                return TypedResults.Conflict("An account with this email already exists.");
            }

            var user = new User
            {
                DisplayName = request.DisplayName.Trim(),
                Email = normalizedEmail,
                IsEmailVerified = false
            };
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            db.Users.Add(user);
            user.Roles.Add(new UserRole { User = user, RoleName = AppRoles.Member });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (UniqueConstraintGuard.IsUniqueConstraintViolation(exception))
            {
                // Two requests could pass the pre-check above concurrently; the
                // unique email index stays authoritative. Keep the response the
                // same as the pre-check so the outcome is consistent.
                return TypedResults.Conflict("An account with this email already exists.");
            }
            var rawToken = await CreateEmailVerificationTokenAsync(user, db, cancellationToken);
            await SendVerificationEmailSafelyAsync(user, rawToken, db, emailSender, appConfiguration,
                environment, loggerFactory, cancellationToken);
            return TypedResults.Ok(new RegistrationResponse(
                "Account created. Check your email for a verification link before signing in.",
                environment.IsDevelopment() ? rawToken : null));
        })
        .WithName("Register")
        .RequireRateLimiting("auth-register");

        group.MapPost("/login", async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult, ForbidHttpResult>>
            (LoginRequest request, MyProjectDbContext db, IPasswordHasher<User> hasher, ILoginAttemptTracker attemptTracker, CancellationToken cancellationToken) =>
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (attemptTracker.IsLocked(normalizedEmail))
            {
                // Respond exactly like a failed login so a throttled account is
                // indistinguishable from one with the wrong password. No DB lookup
                // or hashing is performed for locked accounts.
                return TypedResults.Unauthorized();
            }

            var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);
            if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                // Only lock accounts that actually exist; arbitrary email addresses
                // must not be tracked (otherwise they would fill memory and reveal
                // account existence through the lockout state).
                if (user is not null)
                {
                    attemptTracker.RecordFailure(normalizedEmail);
                }

                return TypedResults.Unauthorized();
            }

            attemptTracker.Reset(normalizedEmail);
            if (!user.IsEmailVerified)
            {
                return TypedResults.Forbid();
            }

            await ApplyBootstrapAdminAsync(user, db, configuration, cancellationToken);
            if (!await db.UserRoles.AnyAsync(userRole => userRole.UserId == user.Id, cancellationToken))
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = AppRoles.Member });
                await db.SaveChangesAsync(cancellationToken);
            }

            return TypedResults.Ok(await CreateResponseAsync(user, db, configuration, cancellationToken));
        })
        .WithName("Login")
        .RequireRateLimiting("auth-login");

        group.MapPost("/verify-email", async Task<Results<NoContent, BadRequest<string>>>
            (VerifyEmailRequest request, MyProjectDbContext db, IConfiguration appConfiguration,
                CancellationToken cancellationToken) =>
        {
            var tokenHash = HashToken(request.Token);
            var now = DateTimeOffset.UtcNow;
            var verification = await db.EmailVerificationTokens
                .Include(candidate => candidate.User)
                .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash
                    && candidate.UsedAt == null, cancellationToken);
            if (verification is null || verification.ExpiresAt <= now)
            {
                return TypedResults.BadRequest("The verification link is invalid or has expired.");
            }

            verification.UsedAt = now;
            verification.User.IsEmailVerified = true;
            await db.SaveChangesAsync(cancellationToken);
            await ApplyBootstrapAdminAsync(verification.User, db, appConfiguration, cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("VerifyEmail");

        group.MapPost("/resend-verification", async Task<Ok<EmailVerificationResponse>>
            (ResendVerificationRequest request, MyProjectDbContext db, IEmailSender emailSender,
                IConfiguration appConfiguration, IHostEnvironment environment, ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);
            string? developmentToken = null;
            if (user is not null && !user.IsEmailVerified)
            {
                developmentToken = await CreateEmailVerificationTokenAsync(user, db, cancellationToken);
                await SendVerificationEmailSafelyAsync(user, developmentToken, db, emailSender, appConfiguration,
                    environment, loggerFactory, cancellationToken);
                if (!environment.IsDevelopment())
                {
                    developmentToken = null;
                }
            }

            return TypedResults.Ok(new EmailVerificationResponse(
                "If the account exists and needs verification, a verification email has been sent.", developmentToken));
        })
        .WithName("ResendEmailVerification")
        .RequireRateLimiting("email-verification");

        group.MapPost("/forgot-password", async Task<Ok<PasswordResetResponse>>
            (ForgotPasswordRequest request, MyProjectDbContext db, IEmailSender emailSender, IHostEnvironment environment, CancellationToken cancellationToken) =>
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);
            if (user is null)
            {
                return TypedResults.Ok(new PasswordResetResponse("If an account exists, a reset link has been sent."));
            }

            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            db.PasswordResetTokens.RemoveRange(db.PasswordResetTokens.Where(token => token.UserId == user.Id));
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            });
            await db.SaveChangesAsync(cancellationToken);

            var resetUrl = $"{configuration["Email:ClientResetUrl"] ?? "http://localhost:8100/auth/reset-password"}?token={Uri.EscapeDataString(rawToken)}";
            await emailSender.SendPasswordResetAsync(user.Email, resetUrl, cancellationToken);
            var developmentToken = environment.IsDevelopment() ? rawToken : null;
            return TypedResults.Ok(new PasswordResetResponse("If an account exists, a reset link has been sent.", developmentToken));
        }).RequireRateLimiting("password-reset");

        group.MapPost("/reset-password", async Task<Results<NoContent, BadRequest<string>>>
            (ResetPasswordRequest request, MyProjectDbContext db, IPasswordHasher<User> hasher, CancellationToken cancellationToken) =>
        {
            var tokenHash = HashToken(request.Token);
            var now = DateTimeOffset.UtcNow;
            var token = await db.PasswordResetTokens
                .Include(resetToken => resetToken.User)
                .FirstOrDefaultAsync(resetToken => resetToken.TokenHash == tokenHash
                    && resetToken.UsedAt == null, cancellationToken);
            if (token is null || token.ExpiresAt <= now)
            {
                return TypedResults.BadRequest("The reset link is invalid or has expired.");
            }

            token.User.PasswordHash = hasher.HashPassword(token.User, request.Password);
            // Possession of a password-reset link proves access to the mailbox.
            token.User.IsEmailVerified = true;
            token.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        });

        return endpoints;
    }

    private static async Task<string> CreateEmailVerificationTokenAsync(User user, MyProjectDbContext db,
        CancellationToken cancellationToken)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.EmailVerificationTokens.RemoveRange(db.EmailVerificationTokens.Where(token => token.UserId == user.Id));
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    private static async Task SendVerificationEmailSafelyAsync(User user, string rawToken, MyProjectDbContext db,
        IEmailSender emailSender, IConfiguration appConfiguration, IHostEnvironment environment,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var verificationBaseUrl = appConfiguration["Email:ClientVerificationUrl"]
            ?? "http://localhost:8100/auth/verify-email";
        var verificationUrl = $"{verificationBaseUrl}?token={Uri.EscapeDataString(rawToken)}";
        try
        {
            await emailSender.SendEmailVerificationAsync(user.Email, verificationUrl, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggerFactory.CreateLogger("EmailVerification").LogError(exception,
                "Could not send the email verification message to user {UserId}.", user.Id);
            // Keep registration and resend responses generic; the user can request another link.
        }
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static async Task ApplyBootstrapAdminAsync(User user, MyProjectDbContext db,
        IConfiguration configuration, CancellationToken cancellationToken)
    {
        var bootstrapUserIdValue = configuration["Authorization:BootstrapAdminUserId"];
        if (!long.TryParse(bootstrapUserIdValue, out var bootstrapUserId)
            || user.Id != bootstrapUserId
            || await db.UserRoles.AnyAsync(userRole => userRole.UserId == user.Id && userRole.RoleName == AppRoles.Admin, cancellationToken))
        {
            return;
        }

        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = AppRoles.Admin });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<AuthResponse> CreateResponseAsync(User user, MyProjectDbContext db,
        IConfiguration configuration, CancellationToken cancellationToken)
    {
        var key = configuration["Jwt:Key"]
            ?? "development-only-change-this-key-before-deployment-geoscenery";
        var issuer = configuration["Jwt:Issuer"] ?? "GeoScenery";
        var audience = configuration["Jwt:Audience"] ?? "GeoScenery.Client";
        var expirationHours = configuration.GetValue("Jwt:ExpirationHours", 8);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };
        var roles = await db.UserRoles.AsNoTracking()
            .Where(userRole => userRole.UserId == user.Id)
            .Select(userRole => userRole.RoleName)
            .ToListAsync(cancellationToken);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.AddRange(AppPermissions.ForRole(role).Select(permission => new Claim("permission", permission)));
        }
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddHours(expirationHours), signingCredentials: credentials);
        return new AuthResponse(user.Id, user.DisplayName, user.Email, new JwtSecurityTokenHandler().WriteToken(token));
    }
}