using System.Security.Claims;
using GeoScenery.Api.Auth;
using GeoScenery.Api.ViewModels;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var userReports = endpoints.MapGroup("/api/users")
            .RequireAuthorization()
            .RequireRateLimiting("content-report");
        userReports.MapPost("/{targetId:long}/reports", async Task<Results<Created<ContentReportResponse>, NotFound, BadRequest<string>>>
            (long targetId, CreateContentReportRequest request, ClaimsPrincipal principal, MyProjectDbContext db,
                IEmailSender emailSender, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            var reporterId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return TypedResults.BadRequest("Enter a description of the violation.");
            }

            if (targetId == reporterId)
            {
                return TypedResults.BadRequest("You cannot report your own profile.");
            }

            var target = await db.Users.AsNoTracking()
                .Where(user => user.Id == targetId)
                .Select(user => user.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
            if (target is null)
            {
                return TypedResults.NotFound();
            }

            var report = await CreateReportAsync(reporterId, ContentReportTargets.Profile, targetId, target,
                request, db, emailSender, loggerFactory, cancellationToken);
            return TypedResults.Created($"/api/reports/{report.Id}",
                new ContentReportResponse(report.Id, report.TargetType, report.TargetId, report.CreatedAt));
        })
        .WithName("ReportUserProfile");

        var sceneReports = endpoints.MapGroup("/api/scenes")
            .RequireAuthorization()
            .RequireRateLimiting("content-report");
        sceneReports.MapPost("/{targetId:long}/reports", async Task<Results<Created<ContentReportResponse>, NotFound, BadRequest<string>>>
            (long targetId, CreateContentReportRequest request, ClaimsPrincipal principal, MyProjectDbContext db,
                IEmailSender emailSender, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            var reporterId = GetUserId(principal);
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return TypedResults.BadRequest("Enter a description of the violation.");
            }

            var target = await db.Scenes.AsNoTracking()
                .Where(scene => scene.Id == targetId)
                .Select(scene => new { scene.Title, scene.OwnerUserId })
                .FirstOrDefaultAsync(cancellationToken);
            if (target is null)
            {
                return TypedResults.NotFound();
            }

            if (target.OwnerUserId == reporterId)
            {
                return TypedResults.BadRequest("You cannot report your own scene.");
            }

            var report = await CreateReportAsync(reporterId, ContentReportTargets.Scene, targetId, target.Title,
                request, db, emailSender, loggerFactory, cancellationToken);
            return TypedResults.Created($"/api/reports/{report.Id}",
                new ContentReportResponse(report.Id, report.TargetType, report.TargetId, report.CreatedAt));
        })
        .WithName("ReportScene");

        return endpoints;
    }

    private static async Task<ContentReport> CreateReportAsync(long reporterId, string targetType, long targetId,
        string targetLabel, CreateContentReportRequest request, MyProjectDbContext db, IEmailSender emailSender,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var description = request.Description.Trim();

        var reporter = await db.Users.AsNoTracking()
            .Where(user => user.Id == reporterId)
            .Select(user => new { user.DisplayName, user.Email })
            .FirstAsync(cancellationToken);
        var report = new ContentReport
        {
            ReporterId = reporterId,
            ReporterDisplayName = reporter.DisplayName,
            ReporterEmail = reporter.Email,
            TargetType = targetType,
            TargetId = targetId,
            TargetLabel = targetLabel,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ContentReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        var adminEmails = await db.UserRoles.AsNoTracking()
            .Where(userRole => userRole.RoleName == AppRoles.Admin)
            .Select(userRole => userRole.User.Email)
            .Distinct()
            .ToListAsync(cancellationToken);
        var logger = loggerFactory.CreateLogger("ContentReports");
        if (adminEmails.Count == 0)
        {
            logger.LogError("Content report {ReportId} was saved but no administrator is assigned to receive it.", report.Id);
            return report;
        }

        var notification = new ContentReportNotification(report.Id, report.TargetType, report.TargetId,
            report.TargetLabel, report.ReporterDisplayName, report.ReporterEmail, report.Description, report.CreatedAt);
        foreach (var adminEmail in adminEmails)
        {
            try
            {
                await emailSender.SendContentReportNotificationAsync(adminEmail, notification, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Unable to send content report {ReportId} notification to an administrator.", report.Id);
            }
        }

        return report;
    }

    private static long GetUserId(ClaimsPrincipal principal) =>
        long.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user id is missing."));
}