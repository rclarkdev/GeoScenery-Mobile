using System.Net;
using System.Net.Sockets;
using System.Text;
using GeoScenery.Api.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GeoScenery.Tests;

[TestFixture]
public sealed class SmtpEmailSenderTests
{
    [Test]
    public async Task GivenConfiguredSmtp_WhenSendingVerification_ThenItSubmitsTheMessage()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string? receivedMessage = null;
        var serverTask = Task.Run(async () =>
        {
            using var connection = await listener.AcceptTcpClientAsync();
            using var stream = connection.GetStream();
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using var reader = new StreamReader(stream, utf8, leaveOpen: true);
            using var writer = new StreamWriter(stream, utf8, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };
            await writer.WriteLineAsync("220 localhost ESMTP");

            while (await reader.ReadLineAsync() is { } command)
            {
                if (command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250-localhost");
                    await writer.WriteLineAsync("250 AUTH PLAIN LOGIN");
                }
                else if (command.StartsWith("AUTH ", StringComparison.OrdinalIgnoreCase))
                {
                    if (command.Equals("AUTH PLAIN", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("334 ");
                        await reader.ReadLineAsync();
                    }
                    await writer.WriteLineAsync("235 Authentication successful");
                }
                else if (command.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase)
                    || command.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250 OK");
                }
                else if (command.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    var message = new StringBuilder();
                    while (await reader.ReadLineAsync() is { } line && line != ".")
                    {
                        message.AppendLine(line);
                    }
                    receivedMessage = message.ToString();
                    await writer.WriteLineAsync("250 Message accepted");
                }
                else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 Bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 OK");
                }
            }
        });

        var sender = CreateSender(new EmailSettings
        {
            SmtpClient = "localhost",
            SmtpPort = port,
            NetworkCredentials = new NetworkCredentials
            {
                Username = "sender@example.com",
                Password = "test-password"
            }
        });

        var outcome = await sender.SendEmailVerificationAsync("recipient@example.com",
            "https://client.example/auth/verify-email?token=sample-token");
        await serverTask;

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(EmailDeliveryOutcome.Sent));
            Assert.That(receivedMessage, Does.Contain("To: recipient@example.com"));
            Assert.That(receivedMessage, Does.Contain("Verify your GeoScenery email address"));
            Assert.That(receivedMessage, Does.Contain("https://client.example/auth/verify-email?token=sample-token"));
        });
    }

    [Test]
    public async Task GivenMissingSmtpSettingsInDevelopment_WhenSending_ThenDeliveryIsReportedAsSkipped()
    {
        var sender = new SmtpEmailSender(Options.Create(new EmailSettings()),
            new TestHostEnvironment(), NullLogger<SmtpEmailSender>.Instance);

        var outcome = await sender.SendEmailVerificationAsync("recipient@example.com", "https://client.example/verify");

        Assert.That(outcome, Is.EqualTo(EmailDeliveryOutcome.SkippedDevelopment));
    }

    private static SmtpEmailSender CreateSender(EmailSettings settings)
    {
        return new SmtpEmailSender(Options.Create(settings), new TestHostEnvironment(Environments.Production),
            NullLogger<SmtpEmailSender>.Instance);
    }

    private sealed class TestHostEnvironment(string environmentName = "Development") : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GeoScenery.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
