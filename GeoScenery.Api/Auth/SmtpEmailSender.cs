using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GeoScenery.Api.Auth;

public sealed class EmailSettings
{
    public string SmtpClient { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public NetworkCredentials NetworkCredentials { get; set; } = new();
}

public sealed class NetworkCredentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class SmtpEmailSender(
    IOptions<EmailSettings> emailSettings,
    IHostEnvironment environment,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _emailSettings = emailSettings.Value;

    public Task<EmailDeliveryOutcome> SendEmailVerificationAsync(string recipient, string verificationUrl,
        CancellationToken cancellationToken = default)
    {
        return SendEmailAsync(recipient, "Verify your GeoScenery email address",
            $"Confirm your email address by opening this link. It expires in 24 hours:\n\n{verificationUrl}",
            cancellationToken);
    }

    public Task<EmailDeliveryOutcome> SendPasswordResetAsync(string recipient, string resetUrl,
        CancellationToken cancellationToken = default)
    {
        return SendEmailAsync(recipient, "Reset your GeoScenery password",
            $"Use this link to reset your password. It expires in one hour:\n\n{resetUrl}",
            cancellationToken);
    }

    public Task<EmailDeliveryOutcome> SendContentReportNotificationAsync(string recipient,
        ContentReportNotification report, CancellationToken cancellationToken = default)
    {
        var subject = $"GeoScenery {report.TargetType.ToLowerInvariant()} report #{report.ReportId}";
        var body = $"A user submitted a content report.\n\n"
            + $"Report ID: {report.ReportId}\n"
            + $"Reported content: {report.TargetType} #{report.TargetId} — {report.TargetLabel}\n"
            + $"Reported by: {report.ReporterDisplayName} <{report.ReporterEmail}>\n"
            + $"Submitted (UTC): {report.CreatedAt:yyyy-MM-dd HH:mm:ss}\n\n"
            + "Description of the violation:\n"
            + report.Description;
        return SendEmailAsync(recipient, subject, body, cancellationToken);
    }

    public Task<EmailDeliveryOutcome> SendSupportContactAsync(string recipient,
        SupportContactNotification request, CancellationToken cancellationToken = default)
    {
        var body = $"A user sent a support request through GeoScenery.\n\n"
            + $"Name: {request.Name}\n"
            + $"Reply email: {request.Email}\n"
            + $"Topic: {request.Topic}\n"
            + $"Submitted (UTC): {request.SubmittedAt:yyyy-MM-dd HH:mm:ss}\n\n"
            + "Message:\n"
            + request.Message;
        return SendEmailAsync(recipient, $"GeoScenery support: {request.Topic}", body, cancellationToken,
            request.Email);
    }

    private async Task<EmailDeliveryOutcome> SendEmailAsync(string recipient, string subject, string body,
        CancellationToken cancellationToken, string? replyTo = null)
    {
        var settings = _emailSettings;
        if (string.IsNullOrWhiteSpace(settings.SmtpClient)
            || string.IsNullOrWhiteSpace(settings.NetworkCredentials.Username)
            || string.IsNullOrWhiteSpace(settings.NetworkCredentials.Password))
        {
            if (environment.IsDevelopment())
            {
                return EmailDeliveryOutcome.SkippedDevelopment;
            }

            throw new InvalidOperationException(
                "EmailSettings:SmtpClient and EmailSettings:NetworkCredentials:Username/Password must be configured outside Development.");
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.NetworkCredentials.Username));
        message.To.Add(MailboxAddress.Parse(recipient));
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        client.Timeout = 30_000;
        try
        {
            await client.ConnectAsync(settings.SmtpClient, settings.SmtpPort, SecureSocketOptions.Auto,
                cancellationToken);
            await client.AuthenticateAsync(settings.NetworkCredentials.Username,
                settings.NetworkCredentials.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            logger.LogInformation("Email accepted by SMTP server for recipient {Recipient}.", recipient);
            return EmailDeliveryOutcome.Sent;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "SMTP email delivery failed for recipient {Recipient} via {SmtpClient}:{SmtpPort}.",
                recipient, settings.SmtpClient, settings.SmtpPort);
            throw;
        }
    }
}
