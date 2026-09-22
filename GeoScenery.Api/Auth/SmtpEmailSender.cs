using System.Net;
using System.Net.Mail;

namespace GeoScenery.Api.Auth;

public sealed class SmtpEmailSender(IConfiguration configuration, IHostEnvironment environment, ILogger<SmtpEmailSender> logger) : IEmailSender
{
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
            Subject = "Reset your GeoScenery password",
            Body = $"Use this link to reset your password. It expires in one hour:\n\n{resetUrl}",
            IsBodyHtml = false
        };
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}