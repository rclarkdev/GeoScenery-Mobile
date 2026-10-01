using GeoScenery.Api.Auth;
using GeoScenery.Api.ViewModels;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using GeoScenery.Data.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace GeoScenery.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin")
            .RequireAuthorization(AuthorizationPolicies.ManageUserRoles);

        group.MapGet("/access", () => TypedResults.NoContent())
            .WithName("AdminCheckAccess");

        group.MapGet("/users", async Task<Results<Ok<PagedResponse<AdminUserResponse>>, BadRequest<string>>>
            (MyProjectDbContext db, CancellationToken cancellationToken, int page = 1, int pageSize = 25, string? search = null) =>
        {
            if (!IsValidPage(page, pageSize))
            {
                return TypedResults.BadRequest("Page must be positive and pageSize must be between 1 and 100.");
            }

            var query = db.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(user => user.DisplayName.Contains(term) || user.Email.Contains(term));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var users = await query
                .AsNoTracking()
                .OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(user => new AdminUserResponse(
                    user.Id,
                    user.DisplayName,
                    user.Email,
                    user.Roles.Select(userRole => userRole.RoleName).OrderBy(role => role).ToList(),
                    user.IsSuspended, user.SuspendedAt, user.SuspensionReason))
                .ToListAsync(cancellationToken);
            return TypedResults.Ok(new PagedResponse<AdminUserResponse>(users, page, pageSize, totalCount));
        })
        .WithName("AdminListUsers");

        group.MapPut("/users/{userId:long}/roles", async Task<Results<Ok<AdminUserResponse>, NotFound, BadRequest<string>>>
            (long userId, UpdateUserRolesRequest request, ClaimsPrincipal principal, HttpContext context,
                MyProjectDbContext db, CancellationToken cancellationToken) =>
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
            var isAddingAdmin = !user.Roles.Any(userRole => userRole.RoleName == AppRoles.Admin)
                && roleNames.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase);
            var canonicalRequestedRoles = roleNames.Select(role => knownRoles.Single(knownRole =>
                string.Equals(knownRole, role, StringComparison.OrdinalIgnoreCase))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (user.Roles.Select(role => role.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(canonicalRequestedRoles))
            {
                return TypedResults.Ok(new AdminUserResponse(user.Id, user.DisplayName, user.Email,
                    canonicalRequestedRoles.OrderBy(role => role).ToList(), user.IsSuspended, user.SuspendedAt, user.SuspensionReason));
            }
            if (userId == GetUserId(principal) && (isRemovingAdmin || isAddingAdmin))
            {
                return TypedResults.BadRequest("Administrators cannot change their own Admin role.");
            }

            if (isRemovingAdmin)
            {
                var anotherAdminExists = await db.UserRoles.AnyAsync(userRole =>
                    userRole.RoleName == AppRoles.Admin && userRole.UserId != userId
                    && !userRole.User.IsSuspended, cancellationToken);
                if (!anotherAdminExists)
                {
                    return TypedResults.BadRequest("The last administrator cannot be demoted.");
                }
            }

            var beforeRoles = user.Roles.Select(userRole => userRole.RoleName).OrderBy(role => role).ToArray();
            var actor = await GetActorAsync(db, principal, cancellationToken);
            db.UserRoles.RemoveRange(user.Roles);
            foreach (var roleName in roleNames)
            {
                var canonicalRoleName = knownRoles.Single(role => string.Equals(role, roleName, StringComparison.OrdinalIgnoreCase));
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = canonicalRoleName });
            }
            var afterRoles = roleNames.OrderBy(role => role).ToArray();
            var hadAdminRole = beforeRoles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase);
            var hasAdminRole = afterRoles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase);
            var roleAction = !hadAdminRole && hasAdminRole ? "AdminRoleGranted"
                : hadAdminRole && !hasAdminRole ? "AdminRoleRevoked" : "UserRolesUpdated";
            db.AdminActionAudits.Add(CreateAudit(actor, roleAction, "User", user.Id,
                "Administrator changed account roles.", beforeRoles, afterRoles, context));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = new AdminUserResponse(user.Id, user.DisplayName, user.Email,
                roleNames.Select(role => knownRoles.Single(knownRole => string.Equals(knownRole, role, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(role => role)
                    .ToList(), user.IsSuspended, user.SuspendedAt, user.SuspensionReason);
            return TypedResults.Ok(response);
        })
        .WithName("AdminUpdateUserRoles");

        group.MapPut("/users/{userId:long}/suspension", async Task<Results<Ok<AdminUserResponse>, NotFound, BadRequest<string>>>
            (long userId, UpdateUserSuspensionRequest request, ClaimsPrincipal principal, HttpContext context,
                MyProjectDbContext db, CancellationToken cancellationToken) =>
        {
            if (request.IsSuspended && string.IsNullOrWhiteSpace(request.Reason))
            {
                return TypedResults.BadRequest("A reason is required when suspending an account.");
            }

            if (userId == GetUserId(principal))
            {
                return TypedResults.BadRequest("Administrators cannot suspend their own account.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var user = await db.Users.Include(candidate => candidate.Roles)
                .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
            if (user is null)
            {
                return TypedResults.NotFound();
            }

            if (request.IsSuspended && user.Roles.Any(role => role.RoleName == AppRoles.Admin)
                && !await db.UserRoles.AnyAsync(role => role.RoleName == AppRoles.Admin
                    && role.UserId != userId && !role.User.IsSuspended, cancellationToken))
            {
                return TypedResults.BadRequest("The last administrator cannot be suspended.");
            }

            if (user.IsSuspended == request.IsSuspended)
            {
                return TypedResults.Ok(new AdminUserResponse(user.Id, user.DisplayName, user.Email,
                    user.Roles.Select(role => role.RoleName).OrderBy(role => role).ToList(),
                    user.IsSuspended, user.SuspendedAt, user.SuspensionReason));
            }

            var actor = await GetActorAsync(db, principal, cancellationToken);
            var before = new { user.IsSuspended, user.SuspendedAt, user.SuspensionReason };
            user.IsSuspended = request.IsSuspended;
            user.SuspendedAt = request.IsSuspended ? DateTimeOffset.UtcNow : null;
            user.SuspensionReason = request.IsSuspended && !string.IsNullOrWhiteSpace(request.Reason)
                ? request.Reason.Trim()
                : null;
            var actionType = request.IsSuspended ? "AccountSuspended" : "AccountRestored";
            db.AdminActionAudits.Add(CreateAudit(actor, actionType, "User", user.Id,
                user.SuspensionReason, before,
                new { user.IsSuspended, user.SuspendedAt, user.SuspensionReason }, context));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return TypedResults.Ok(new AdminUserResponse(user.Id, user.DisplayName, user.Email,
                user.Roles.Select(role => role.RoleName).OrderBy(role => role).ToList(),
                user.IsSuspended, user.SuspendedAt, user.SuspensionReason));
        })
        .WithName("AdminUpdateUserSuspension");

        group.MapDelete("/users/{userId:long}", async Task<Results<NoContent, NotFound, BadRequest<string>>>
            (long userId, ClaimsPrincipal principal, MyProjectDbContext db, IUserService userService,
                HttpContext context, CancellationToken cancellationToken) =>
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

                if (target.IsAdmin && !await db.UserRoles.AnyAsync(userRole => userRole.RoleName == AppRoles.Admin
                    && userRole.UserId != userId && !userRole.User.IsSuspended, cancellationToken))
            {
                return TypedResults.BadRequest("The last administrator cannot be deleted.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var before = new { target.Id, target.IsAdmin };
            var actor = await GetActorAsync(db, principal, cancellationToken);
            var openReports = await db.ContentReports
                .Where(report => report.TargetType == ContentReportTargets.Profile
                    && report.TargetId == userId
                    && report.Status != ContentReportStatuses.Dismissed
                    && report.Status != ContentReportStatuses.Actioned)
                .ToListAsync(cancellationToken);
            foreach (var report in openReports)
            {
                var reportBefore = new { report.Status, report.ResolvedAt, report.ResolutionNotes, report.ActionTaken };
                report.Status = ContentReportStatuses.Actioned;
                report.ReviewedByUserId = actor.Id;
                report.ReviewedByDisplayName = actor.DisplayName;
                report.ReviewedAt = DateTimeOffset.UtcNow;
                report.ResolvedAt = report.ReviewedAt;
                report.ActionTaken = "AccountDeleted";
                report.ResolutionNotes ??= "Account deleted by an administrator.";
                db.AdminActionAudits.Add(CreateAudit(actor, "ReportActionedByAccountDeletion",
                    "ContentReport", report.Id, report.ResolutionNotes, reportBefore,
                    new { report.Status, report.ResolvedAt, report.ResolutionNotes, report.ActionTaken }, context));
            }
            if (!await userService.DeleteAsync(userId, cancellationToken))
            {
                return TypedResults.BadRequest("The account could not be deleted; verify that another administrator remains.");
            }

            db.AdminActionAudits.Add(CreateAudit(actor, "AccountDeleted", "User", userId,
                "Account deleted from the admin control area.", before, new { deleted = true }, context));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return TypedResults.NoContent();
        })
        .WithName("AdminDeleteUser");

        group.MapGet("/reports", async Task<Results<Ok<PagedResponse<AdminContentReportResponse>>, BadRequest<string>>>
            (MyProjectDbContext db, CancellationToken cancellationToken, string? status = null,
                string? targetType = null, string? search = null, int page = 1, int pageSize = 25) =>
        {
            if (!IsValidPage(page, pageSize))
            {
                return TypedResults.BadRequest("Page must be positive and pageSize must be between 1 and 100.");
            }

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

            if (!string.IsNullOrWhiteSpace(targetType))
            {
                var canonicalTarget = new[] { ContentReportTargets.Profile, ContentReportTargets.Scene }
                    .FirstOrDefault(candidate => string.Equals(candidate, targetType.Trim(), StringComparison.OrdinalIgnoreCase));
                if (canonicalTarget is null)
                {
                    return TypedResults.BadRequest("Target type must be Profile or Scene.");
                }
                query = query.Where(report => report.TargetType == canonicalTarget);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(report => report.TargetLabel.Contains(term)
                    || report.ReporterDisplayName.Contains(term)
                    || report.Description.Contains(term));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var reports = await query
                .OrderByDescending(report => report.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            var sceneIds = reports.Where(report => report.TargetType == ContentReportTargets.Scene)
                .Select(report => report.TargetId).Distinct().ToArray();
            var profileIds = reports.Where(report => report.TargetType == ContentReportTargets.Profile)
                .Select(report => report.TargetId).Distinct().ToArray();
            var existingSceneIds = await db.Scenes.AsNoTracking()
                .Where(scene => sceneIds.Contains(scene.Id))
                .Select(scene => scene.Id).ToListAsync(cancellationToken);
            var hiddenSceneIds = await db.Scenes.AsNoTracking()
                .Where(scene => sceneIds.Contains(scene.Id) && scene.IsHidden)
                .Select(scene => scene.Id).ToListAsync(cancellationToken);
            var existingSceneIdSet = existingSceneIds.ToHashSet();
            var hiddenSceneIdSet = hiddenSceneIds.ToHashSet();
            var profileStateRows = await db.Users.AsNoTracking()
                .Where(user => profileIds.Contains(user.Id))
                .Select(user => new { user.Id, user.IsSuspended })
                .ToListAsync(cancellationToken);
            var profileStateById = profileStateRows.ToDictionary(user => user.Id, user => user.IsSuspended);
            var sceneExists = hiddenSceneIdSet;
            var response = reports.Select(report =>
            {
                var targetExists = report.TargetType == ContentReportTargets.Scene
                    ? existingSceneIdSet.Contains(report.TargetId)
                    : profileStateById.ContainsKey(report.TargetId);
                var targetIsHidden = report.TargetType == ContentReportTargets.Scene
                    && targetExists && hiddenSceneIdSet.Contains(report.TargetId);
                var targetIsSuspended = report.TargetType == ContentReportTargets.Profile
                    && profileStateById.TryGetValue(report.TargetId, out var suspended) && suspended;
                return ToAdminReportResponse(report, targetExists, targetIsHidden, targetIsSuspended);
            }).ToList();
            return TypedResults.Ok(new PagedResponse<AdminContentReportResponse>(response, page, pageSize, totalCount));
        })
        .WithName("AdminListReports");

        group.MapGet("/audit", async Task<Results<Ok<PagedResponse<AdminActionAuditResponse>>, BadRequest<string>>>
            (MyProjectDbContext db, CancellationToken cancellationToken, int page = 1, int pageSize = 25) =>
        {
            if (!IsValidPage(page, pageSize))
            {
                return TypedResults.BadRequest("Page must be positive and pageSize must be between 1 and 100.");
            }

            var totalCount = await db.AdminActionAudits.CountAsync(cancellationToken);
            var entries = await db.AdminActionAudits.AsNoTracking()
                .OrderByDescending(audit => audit.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(audit => new AdminActionAuditResponse(audit.Id, audit.ActorUserId,
                    audit.ActorDisplayName, audit.ActionType, audit.TargetType, audit.TargetId,
                    audit.Reason, audit.BeforeStateJson, audit.AfterStateJson, audit.CorrelationId, audit.CreatedAt))
                .ToListAsync(cancellationToken);
            return TypedResults.Ok(new PagedResponse<AdminActionAuditResponse>(entries, page, pageSize, totalCount));
        })
        .WithName("AdminListAudit");

        group.MapGet("/scenes", async Task<Results<Ok<PagedResponse<AdminSceneResponse>>, BadRequest<string>>>
            (MyProjectDbContext db, CancellationToken cancellationToken, int page = 1, int pageSize = 25,
                string? search = null, bool? isHidden = null) =>
        {
            if (!IsValidPage(page, pageSize))
            {
                return TypedResults.BadRequest("Page must be positive and pageSize must be between 1 and 100.");
            }

            var query = db.Scenes.AsNoTracking();
            if (isHidden.HasValue)
            {
                query = query.Where(scene => scene.IsHidden == isHidden.Value);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(scene => scene.Title.Contains(term) || scene.Description.Contains(term));
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var scenes = await query.OrderByDescending(scene => scene.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(scene => new AdminSceneResponse(scene.Id, scene.Title, scene.OwnerUserId,
                    scene.IsHidden, scene.HiddenAt, scene.HiddenReason))
                .ToListAsync(cancellationToken);
            return TypedResults.Ok(new PagedResponse<AdminSceneResponse>(scenes, page, pageSize, totalCount));
        })
        .WithName("AdminListScenes");

        group.MapPut("/scenes/{sceneId:long}/visibility", async Task<Results<NoContent, NotFound, BadRequest<string>>>
            (long sceneId, UpdateSceneVisibilityRequest request, ClaimsPrincipal principal, HttpContext context,
                MyProjectDbContext db, CancellationToken cancellationToken) =>
        {
            if (request.IsHidden && string.IsNullOrWhiteSpace(request.Reason))
            {
                return TypedResults.BadRequest("A reason is required when hiding a scene.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var scene = await db.Scenes.FirstOrDefaultAsync(candidate => candidate.Id == sceneId, cancellationToken);
            if (scene is null)
            {
                return TypedResults.NotFound();
            }

            if (scene.IsHidden == request.IsHidden)
            {
                return TypedResults.NoContent();
            }

            var actor = await GetActorAsync(db, principal, cancellationToken);
            var before = new { scene.IsHidden, scene.HiddenAt, scene.HiddenReason };
            scene.IsHidden = request.IsHidden;
            scene.HiddenAt = request.IsHidden ? DateTimeOffset.UtcNow : null;
            scene.HiddenReason = request.IsHidden && !string.IsNullOrWhiteSpace(request.Reason)
                ? request.Reason.Trim()
                : null;
            db.AdminActionAudits.Add(CreateAudit(actor, request.IsHidden ? "SceneHidden" : "SceneRestored",
                "Scene", scene.Id, scene.HiddenReason, before,
                new { scene.IsHidden, scene.HiddenAt, scene.HiddenReason }, context));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("AdminUpdateSceneVisibility");

        group.MapPut("/reports/{reportId:long}/status", async Task<Results<Ok<AdminContentReportResponse>, NotFound, BadRequest<string>>>
            (long reportId, UpdateContentReportStatusRequest request, ClaimsPrincipal principal, HttpContext context,
                MyProjectDbContext db,
                CancellationToken cancellationToken) =>
        {
            var status = ContentReportStatuses.All.FirstOrDefault(candidate =>
                string.Equals(candidate, request.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (status is null)
            {
                return TypedResults.BadRequest("Status must be Pending, Reviewed, Dismissed, or Actioned.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var report = await db.ContentReports.FirstOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken);
            if (report is null)
            {
                return TypedResults.NotFound();
            }

            if (string.Equals(report.Status, status, StringComparison.Ordinal))
            {
                return TypedResults.Ok(ToAdminReportResponse(report));
            }

            if (!CanTransitionReport(report.Status, status))
            {
                return TypedResults.BadRequest($"Report status cannot transition from {report.Status} to {status}.");
            }

            if (!string.Equals(report.Status, status, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(request.ResolutionNotes))
            {
                return TypedResults.BadRequest("A resolution note is required when changing a report status.");
            }
            if (status == ContentReportStatuses.Actioned && string.IsNullOrWhiteSpace(request.ActionTaken))
            {
                return TypedResults.BadRequest("Describe the action taken before marking a report actioned.");
            }

            var before = new { report.Status, report.ResolvedAt, report.ResolutionNotes, report.ActionTaken };
            var actor = await GetActorAsync(db, principal, cancellationToken);
            report.Status = status;
            report.ReviewedByUserId = actor.Id;
            report.ReviewedByDisplayName = actor.DisplayName;
            report.ReviewedAt = DateTimeOffset.UtcNow;
            report.ResolvedAt = status is ContentReportStatuses.Dismissed or ContentReportStatuses.Actioned
                ? report.ResolvedAt ?? DateTimeOffset.UtcNow
                : null;
            report.ResolutionNotes = string.IsNullOrWhiteSpace(request.ResolutionNotes)
                ? report.ResolutionNotes
                : request.ResolutionNotes.Trim();
            report.ActionTaken = string.IsNullOrWhiteSpace(request.ActionTaken)
                ? report.ActionTaken
                : request.ActionTaken.Trim();
            db.AdminActionAudits.Add(CreateAudit(actor, "ReportStatusChanged", "ContentReport", report.Id,
                report.ResolutionNotes ?? "Report moderation status changed.", before,
                new { report.Status, report.ResolvedAt, report.ResolutionNotes, report.ActionTaken }, context));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.Ok(ToAdminReportResponse(report));
        })
        .WithName("AdminUpdateReportStatus");

        group.MapDelete("/scenes/{sceneId:long}", async Task<Results<NoContent, NotFound>>
            (long sceneId, ClaimsPrincipal principal, HttpContext context, MyProjectDbContext db,
                CancellationToken cancellationToken) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var scene = await db.Scenes.FirstOrDefaultAsync(candidate => candidate.Id == sceneId, cancellationToken);
            if (scene is null)
            {
                return TypedResults.NotFound();
            }

            var actor = await GetActorAsync(db, principal, cancellationToken);
            var before = new { scene.Id, scene.Title, scene.OwnerUserId };

            db.Scenes.Remove(scene);
            var relatedReports = await db.ContentReports
                .Where(report => report.TargetType == ContentReportTargets.Scene
                    && report.TargetId == sceneId
                    && report.Status != ContentReportStatuses.Dismissed
                    && report.Status != ContentReportStatuses.Actioned)
                .ToListAsync(cancellationToken);
            foreach (var report in relatedReports)
            {
                var reportBefore = new { report.Status, report.ResolvedAt, report.ResolutionNotes, report.ActionTaken };
                report.Status = ContentReportStatuses.Actioned;
                report.ReviewedByUserId = actor.Id;
                report.ReviewedByDisplayName = actor.DisplayName;
                report.ReviewedAt = DateTimeOffset.UtcNow;
                report.ActionTaken = "SceneDeleted";
                report.ResolutionNotes ??= "Scene deleted by an administrator.";
                report.ResolvedAt = report.ReviewedAt;
                db.AdminActionAudits.Add(CreateAudit(actor, "ReportActionedBySceneDeletion",
                    "ContentReport", report.Id, report.ResolutionNotes, reportBefore,
                    new { report.Status, report.ResolvedAt, report.ResolutionNotes, report.ActionTaken }, context));
            }

            db.AdminActionAudits.Add(CreateAudit(actor, "SceneDeleted", "Scene", sceneId,
                "Scene removed by an administrator.", before, new { deleted = true }, context));

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("AdminDeleteScene");

        return endpoints;
    }

    private static AdminContentReportResponse ToAdminReportResponse(ContentReport report,
        bool targetExists = true, bool targetIsHidden = false, bool targetIsSuspended = false) =>
        new(report.Id, report.TargetType, report.TargetId, report.TargetLabel, report.ReporterDisplayName,
            report.ReporterEmail, report.Description, report.Status, report.CreatedAt,
            report.ReviewedByUserId, report.ReviewedByDisplayName, report.ReviewedAt,
            report.ResolvedAt, report.ResolutionNotes, report.ActionTaken, targetExists, targetIsHidden, targetIsSuspended);

    private static AdminSceneResponse ToAdminSceneResponse(Scene scene) =>
        new(scene.Id, scene.Title, scene.OwnerUserId, scene.IsHidden, scene.HiddenAt, scene.HiddenReason);

    private static bool CanTransitionReport(string current, string requested)
    {
        if (string.Equals(current, requested, StringComparison.Ordinal))
        {
            return true;
        }

        return current switch
        {
            ContentReportStatuses.Pending => requested is ContentReportStatuses.Reviewed or ContentReportStatuses.Dismissed or ContentReportStatuses.Actioned,
            ContentReportStatuses.Reviewed => requested is ContentReportStatuses.Dismissed or ContentReportStatuses.Actioned,
            _ => false
        };
    }

    private static bool IsValidPage(int page, int pageSize) =>
        page > 0 && pageSize is > 0 and <= 100 && (long)(page - 1) * pageSize <= int.MaxValue;

    private static async Task<AuditActor> GetActorAsync(MyProjectDbContext db, ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AuditActor(user.Id, user.DisplayName, user.Email))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The acting administrator account no longer exists.");
    }

    private static AdminActionAudit CreateAudit(AuditActor actor, string actionType, string targetType,
        long? targetId, string? reason, object? beforeState, object? afterState, HttpContext context) => new()
    {
        ActorUserId = actor.Id,
        ActorDisplayName = actor.DisplayName,
        ActorEmail = actor.Email,
        ActionType = actionType,
        TargetType = targetType,
        TargetId = targetId,
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        BeforeStateJson = SerializeAuditState(beforeState),
        AfterStateJson = SerializeAuditState(afterState),
        CorrelationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault() ?? context.TraceIdentifier
    };

    private static string? SerializeAuditState(object? value)
    {
        if (value is null) return null;
        var json = JsonSerializer.Serialize(value);
        return json.Length <= 4000 ? json : json[..4000];
    }

    private sealed record AuditActor(long Id, string DisplayName, string Email);

    private static long GetUserId(ClaimsPrincipal principal) =>
        long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated administrator id is missing."));
}