using GeoScenery.Api.Auth;
using GeoScenery.Api.ViewModels;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GeoScenery.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin")
            .RequireAuthorization(AuthorizationPolicies.ManageUserRoles);

        group.MapGet("/users", async Task<Ok<IReadOnlyList<AdminUserResponse>>>
            (MyProjectDbContext db, CancellationToken cancellationToken) =>
        {
            var users = await db.Users
                .AsNoTracking()
                .OrderBy(user => user.DisplayName)
                .Select(user => new AdminUserResponse(
                    user.Id,
                    user.DisplayName,
                    user.Email,
                    user.Roles.Select(userRole => userRole.RoleName).OrderBy(role => role).ToList()))
                .ToListAsync(cancellationToken);
            return TypedResults.Ok<IReadOnlyList<AdminUserResponse>>(users);
        })
        .WithName("AdminListUsers");

        group.MapPut("/users/{userId:long}/roles", async Task<Results<Ok<AdminUserResponse>, NotFound, BadRequest<string>>>
            (long userId, UpdateUserRolesRequest request, MyProjectDbContext db, CancellationToken cancellationToken) =>
        {
            var roleNames = request.Roles
                .Where(role => role is not null)
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var knownRoles = new[] { AppRoles.Member, AppRoles.Admin };
            if (roleNames.Count == 0 || roleNames.Any(role => !knownRoles.Contains(role, StringComparer.OrdinalIgnoreCase)))
            {
                return TypedResults.BadRequest("Assign at least one valid role: Member or Admin.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var user = await db.Users
                .Include(candidate => candidate.Roles)
                .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
            if (user is null)
            {
                return TypedResults.NotFound();
            }

            var isRemovingAdmin = user.Roles.Any(userRole => userRole.RoleName == AppRoles.Admin)
                && !roleNames.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase);
            if (isRemovingAdmin)
            {
                var anotherAdminExists = await db.UserRoles.AnyAsync(userRole =>
                    userRole.RoleName == AppRoles.Admin && userRole.UserId != userId, cancellationToken);
                if (!anotherAdminExists)
                {
                    return TypedResults.BadRequest("The last administrator cannot be demoted.");
                }
            }

            db.UserRoles.RemoveRange(user.Roles);
            foreach (var roleName in roleNames)
            {
                var canonicalRoleName = knownRoles.Single(role => string.Equals(role, roleName, StringComparison.OrdinalIgnoreCase));
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = canonicalRoleName });
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new AdminUserResponse(user.Id, user.DisplayName, user.Email,
                roleNames.Select(role => knownRoles.Single(knownRole => string.Equals(knownRole, role, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(role => role)
                    .ToList());
            return TypedResults.Ok(response);
        })
        .WithName("AdminUpdateUserRoles");

        return endpoints;
    }
}