using System.Net;
using System.Net.Mail;

namespace GeoScenery.Api.Auth;

public sealed class SmtpEmailSender(IConfiguration configuration, IHostEnvironment environment, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailVerificationAsync(string recipient, string verificationUrl, CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Email");
        var host = section["SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation("Email verification link for {Recipient}: {VerificationUrl}", recipient, verificationUrl);
                return;
            }

            throw new InvalidOperationException("Email:SmtpHost must be configured outside Development.");
        }

        using var client = CreateClient(section, host);
        using var message = new MailMessage(section["From"] ?? "no-reply@geoscenery.local", recipient)
        {
            Subject = "Verify your GeoScenery email address",
            Body = $"Confirm your email address by opening this link. It expires in 24 hours:\n\n{verificationUrl}",
            IsBodyHtml = false
        };
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Email");
        var host = section["SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation("Password reset link for {Recipient}: {ResetUrl}", recipient, resetUrl);
                return;
            }

            throw new InvalidOperationException("Email:SmtpHost must be configured outside Development.");
        }

        using var client = CreateClient(section, host);

        using var message = new MailMessage(section["From"] ?? "no-reply@geoscenery.local", recipient)
        {
            Subject = "Reset your GeoScenery password",
            Body = $"Use this link to reset your password. It expires in one hour:\n\n{resetUrl}",
            IsBodyHtml = false
        };
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }

    private static SmtpClient CreateClient(IConfigurationSection section, string host)
    {
        var client = new SmtpClient(host, section.GetValue("SmtpPort", 587))
        {
            EnableSsl = section.GetValue("EnableSsl", true)
        };
        var username = section["Username"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, section["Password"]);
        }

        return client;
    }

    public async Task SendContentReportNotificationAsync(string recipient, ContentReportNotification report,
        CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Email");
        var host = section["SmtpHost"];
        var subject = $"GeoScenery {report.TargetType.ToLowerInvariant()} report #{report.ReportId}";
        var body = $"A user submitted a content report.\n\n"
            + $"Report ID: {report.ReportId}\n"
            + $"Reported content: {report.TargetType} #{report.TargetId} — {report.TargetLabel}\n"
            + $"Reported by: {report.ReporterDisplayName} <{report.ReporterEmail}>\n"
            + $"Submitted (UTC): {report.CreatedAt:yyyy-MM-dd HH:mm:ss}\n\n"
            + "Description of the violation:\n"
            + report.Description;

        if (string.IsNullOrWhiteSpace(host))
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation("Content report notification for {Recipient}: {Subject}\n{ReportBody}", recipient, subject, body);
                return;
            }

            throw new InvalidOperationException("Email:SmtpHost must be configured outside Development.");
        }

        using var client = new SmtpClient(host, section.GetValue("SmtpPort", 587))
        {
            EnableSsl = section.GetValue("EnableSsl", true)
        };
        var username = section["Username"];
        var password = section["Password"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var message = new MailMessage(section["From"] ?? "no-reply@geoscenery.local", recipient)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}