using System.IdentityModel.Tokens.Jwt;
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

        return endpoints;
    }

    private static AuthResponse CreateResponse(User user, IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "GeoScenery";
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, issuer, claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
        return new AuthResponse(user.Id, user.DisplayName, user.Email, new JwtSecurityTokenHandler().WriteToken(token));
    }
}