namespace GeoScenery.Api.Auth;

public enum EmailDeliveryOutcome
{
    Sent,
    SkippedDevelopment,
    Failed
}

public interface IEmailSender
{
    Task<EmailDeliveryOutcome> SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken = default);

    Task<EmailDeliveryOutcome> SendEmailVerificationAsync(string recipient, string verificationUrl, CancellationToken cancellationToken = default);

    Task<EmailDeliveryOutcome> SendContentReportNotificationAsync(string recipient, ContentReportNotification report,
        CancellationToken cancellationToken = default);

    Task<EmailDeliveryOutcome> SendSupportContactAsync(string recipient, SupportContactNotification request,
        CancellationToken cancellationToken = default);
}