using System.ComponentModel.DataAnnotations;
using GeoScenery.Api.Auth;
using GeoScenery.Api.Logging;
using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Api.Endpoints;

public static class SupportEndpoints
{
    public static IEndpointRouteBuilder MapSupportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/support/contact", async Task<Results<Ok<SupportContactResponse>, BadRequest<string>, StatusCodeHttpResult>>
            (SupportContactRequest request, MyProjectDbContext db, IEmailSender emailSender, HttpContext context,
                CancellationToken cancellationToken) =>
        {
            // Bot trap: silently accept but do not deliver submissions that fill
            // the field intended to remain hidden from real users.
            if (!string.IsNullOrWhiteSpace(request.Website))
            {
                return TypedResults.Ok(new SupportContactResponse("Thanks. Your message has been received."));
            }

            var name = request.Name.Trim();
            var email = request.Email.Trim();
            var topic = request.Topic.Trim();
            var message = request.Message.Trim();
            if (name.Length == 0 || email.Length == 0 || topic.Length == 0 || message.Length < 10)
            {
                return TypedResults.BadRequest("Complete each field and enter a message of at least 10 characters.");
            }

            var recipients = await db.UserRoles.AsNoTracking()
                .Where(userRole => userRole.RoleName == AppRoles.Admin
                    && userRole.User.IsEmailVerified
                    && !userRole.User.IsSuspended)
                .Select(userRole => userRole.User.Email)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (recipients.Count == 0)
            {
                RequestAuditContext.Set(context, "operation", "support-contact");
                RequestAuditContext.Set(context, "operationOutcome", "no-admin-recipients");
                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var notification = new SupportContactNotification(name, email, topic, message, DateTimeOffset.UtcNow);
            var sent = 0;
            var skipped = 0;
            var failed = 0;
            string? failureType = null;
            foreach (var recipient in recipients)
            {
                try
                {
                    var outcome = await emailSender.SendSupportContactAsync(recipient, notification, cancellationToken);
                    if (outcome == EmailDeliveryOutcome.Sent)
                    {
                        sent++;
                    }
                    else if (outcome == EmailDeliveryOutcome.SkippedDevelopment)
                    {
                        skipped++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                    failureType ??= exception.GetType().Name;
                }
            }

            RequestAuditContext.Set(context, "operation", "support-contact");
            RequestAuditContext.Set(context, "notificationRecipients", recipients.Count);
            RequestAuditContext.Set(context, "notificationsSent", sent);
            RequestAuditContext.Set(context, "notificationsSkippedDevelopment", skipped);
            RequestAuditContext.Set(context, "notificationFailures", failed);
            RequestAuditContext.Set(context, "notificationFailureType", failureType);
            RequestAuditContext.Set(context, "operationOutcome", sent > 0 ? "Success"
                : skipped == recipients.Count ? "skipped-development" : "Failure");

            if (sent == 0)
            {
                return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return TypedResults.Ok(new SupportContactResponse("Your message was sent to the GeoScenery administrators."));
        })
        .WithName("ContactSupport")
        .WithSummary("Send a support request to GeoScenery administrators")
        .WithDescription("Accepts a rate-limited support request and emails active, verified administrator accounts.")
        .Produces<SupportContactResponse>(StatusCodes.Status200OK)
        .Produces<string>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .RequireRateLimiting("support-contact");

        return endpoints;
    }
}

/// <summary>Message submitted through the public support contact form.</summary>
public sealed record SupportContactRequest
{
    /// <summary>Name to include in the message to administrators.</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>Email address administrators may use to reply.</summary>
    [Required, EmailAddress, StringLength(320, MinimumLength = 3)]
    public required string Email { get; init; }

    /// <summary>Short category describing the request.</summary>
    [Required, StringLength(80, MinimumLength = 1)]
    public required string Topic { get; init; }

    /// <summary>Details of the support request.</summary>
    [Required, StringLength(4000, MinimumLength = 10)]
    public required string Message { get; init; }

    /// <summary>Spam prevention field that must remain empty.</summary>
    [StringLength(200)]
    public string? Website { get; init; }
}
