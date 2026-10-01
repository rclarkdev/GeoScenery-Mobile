using GeoScenery.Data.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GeoScenery.Data.Context;

namespace GeoScenery.Api.Auth;

public static class AuthorizationPolicies
{
    public const string ManageUserRoles = "ManageUserRoles";
}

public static class AppPermissions
{
    public const string ManageUserRoles = "users.roles.manage";

    public static IReadOnlyCollection<string> ForRole(string roleName) => roleName switch
    {
        AppRoles.Admin => [ManageUserRoles],
        _ => []
    };
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Checks the current database role assignments so demoted users lose access immediately.</summary>
public sealed class PermissionAuthorizationHandler(MyProjectDbContext db)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var roleNames = await db.UserRoles.AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.RoleName)
            .ToListAsync();
        if (roleNames.Any(role => AppPermissions.ForRole(role).Contains(requirement.Permission)))
        {
            context.Succeed(requirement);
        }
    }
}