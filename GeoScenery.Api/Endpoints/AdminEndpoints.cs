using GeoScenery.Api.Auth;
using GeoScenery.Api.ViewModels;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using GeoScenery.Data.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

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

        group.MapDelete("/users/{userId:long}", async Task<Results<NoContent, NotFound, BadRequest<string>>>
            (long userId, ClaimsPrincipal principal, MyProjectDbContext db, IUserService userService,
                CancellationToken cancellationToken) =>
        {
            if (userId == GetUserId(principal))
            {
                return TypedResults.BadRequest("Use account settings to delete your own account.");
            }

            var target = await db.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.Id, IsAdmin = user.Roles.Any(role => role.RoleName == AppRoles.Admin) })
                .FirstOrDefaultAsync(cancellationToken);
            if (target is null)
            {
                return TypedResults.NotFound();
            }

            if (target.IsAdmin && await db.UserRoles.CountAsync(
                    userRole => userRole.RoleName == AppRoles.Admin, cancellationToken) <= 1)
            {
                return TypedResults.BadRequest("The last administrator cannot be deleted.");
            }

            if (!await userService.DeleteAsync(userId, cancellationToken))
            {
                return TypedResults.BadRequest("The account could not be deleted; verify that another administrator remains.");
            }

            return TypedResults.NoContent();
        })
        .WithName("AdminDeleteUser");

        group.MapGet("/reports", async Task<Results<Ok<IReadOnlyList<AdminContentReportResponse>>, BadRequest<string>>>
            (string? status, MyProjectDbContext db, CancellationToken cancellationToken) =>
        {
            var query = db.ContentReports.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
            {
                var canonicalStatus = ContentReportStatuses.All.FirstOrDefault(candidate =>
                    string.Equals(candidate, status.Trim(), StringComparison.OrdinalIgnoreCase));
                if (canonicalStatus is null)
                {
                    return TypedResults.BadRequest("Status must be Pending, Reviewed, Dismissed, or Actioned.");
                }

                query = query.Where(report => report.Status == canonicalStatus);
            }

            var reports = await query.ToListAsync(cancellationToken);
            var response = reports
                .OrderByDescending(report => report.CreatedAt)
                .Select(ToAdminReportResponse)
                .ToList();
            return TypedResults.Ok<IReadOnlyList<AdminContentReportResponse>>(response);
        })
        .WithName("AdminListReports");

        group.MapPut("/reports/{reportId:long}/status", async Task<Results<Ok<AdminContentReportResponse>, NotFound, BadRequest<string>>>
            (long reportId, UpdateContentReportStatusRequest request, MyProjectDbContext db,
                CancellationToken cancellationToken) =>
        {
            var status = ContentReportStatuses.All.FirstOrDefault(candidate =>
                string.Equals(candidate, request.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (status is null)
            {
                return TypedResults.BadRequest("Status must be Pending, Reviewed, Dismissed, or Actioned.");
            }

            var report = await db.ContentReports.FirstOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken);
            if (report is null)
            {
                return TypedResults.NotFound();
            }

            report.Status = status;
            report.ResolvedAt = status is ContentReportStatuses.Dismissed or ContentReportStatuses.Actioned
                ? DateTimeOffset.UtcNow
                : null;
            report.ResolutionNotes = string.IsNullOrWhiteSpace(request.ResolutionNotes)
                ? null
                : request.ResolutionNotes.Trim();
            report.ActionTaken = string.IsNullOrWhiteSpace(request.ActionTaken) ? null : request.ActionTaken.Trim();
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Ok(ToAdminReportResponse(report));
        })
        .WithName("AdminUpdateReportStatus");

        group.MapDelete("/scenes/{sceneId:long}", async Task<Results<NoContent, NotFound>>
            (long sceneId, MyProjectDbContext db, CancellationToken cancellationToken) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var scene = await db.Scenes.FirstOrDefaultAsync(candidate => candidate.Id == sceneId, cancellationToken);
            if (scene is null)
            {
                return TypedResults.NotFound();
            }

            db.Scenes.Remove(scene);
            var relatedReports = await db.ContentReports
                .Where(report => report.TargetType == ContentReportTargets.Scene
                    && report.TargetId == sceneId
                    && report.Status != ContentReportStatuses.Dismissed
                    && report.Status != ContentReportStatuses.Actioned)
                .ToListAsync(cancellationToken);
            foreach (var report in relatedReports)
            {
                report.Status = ContentReportStatuses.Actioned;
                report.ActionTaken = "SceneDeleted";
                report.ResolutionNotes ??= "Scene deleted by an administrator.";
                report.ResolvedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("AdminDeleteScene");

        return endpoints;
    }

    private static AdminContentReportResponse ToAdminReportResponse(ContentReport report) =>
        new(report.Id, report.TargetType, report.TargetId, report.TargetLabel, report.ReporterDisplayName,
            report.ReporterEmail, report.Description, report.Status, report.CreatedAt,
            report.ResolvedAt, report.ResolutionNotes, report.ActionTaken);

    private static long GetUserId(ClaimsPrincipal principal) =>
        long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated administrator id is missing."));
}