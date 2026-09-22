using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
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

        group.MapPost("/register", async Task<Results<Ok<AuthResponse>, Conflict<string>>>
            (RegisterRequest request, MyProjectDbContext db, IPasswordHasher<User> hasher, CancellationToken cancellationToken) =>
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
            {
                return TypedResults.Conflict("An account with this email already exists.");
            }

            var user = new User
            {
                DisplayName = request.DisplayName.Trim(),
                Email = normalizedEmail
            };
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(CreateResponse(user, configuration));
        });

        group.MapPost("/login", async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>>
            (LoginRequest request, MyProjectDbContext db, IPasswordHasher<User> hasher, CancellationToken cancellationToken) =>
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);
            if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                return TypedResults.Unauthorized();
            }

            return TypedResults.Ok(CreateResponse(user, configuration));
        });

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
        });

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
            token.UsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        });

        return endpoints;
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static AuthResponse CreateResponse(User user, IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "GeoScenery";
        var audience = configuration["Jwt:Audience"] ?? "GeoScenery.Client";
        var expirationHours = configuration.GetValue("Jwt:ExpirationHours", 8);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddHours(expirationHours), signingCredentials: credentials);
        return new AuthResponse(user.Id, user.DisplayName, user.Email, new JwtSecurityTokenHandler().WriteToken(token));
    }
}