namespace GeoScenery.Api.Auth;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken = default);

    Task SendEmailVerificationAsync(string recipient, string verificationUrl, CancellationToken cancellationToken = default);

    Task SendContentReportNotificationAsync(string recipient, ContentReportNotification report,
        CancellationToken cancellationToken = default);
}