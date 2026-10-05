using GeoScenery.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using GeoScenery.Data.Models;
using GeoScenery.Api.Auth;

namespace GeoScenery.Tests;

public sealed class GeoSceneryApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public string? LastResetUrl { get; private set; }
    public string? LastVerificationUrl { get; private set; }
    public bool FailReportNotifications { get; set; }
    public bool FailVerificationEmails { get; set; }
    public List<(string Recipient, ContentReportNotification Report)> ReportNotifications { get; } = [];
    public List<(string Recipient, SupportContactNotification Request)> SupportNotifications { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Authorization:BootstrapAdminUserId", "2");
        // Enable forwarded header handling in the test host so tests can simulate
        // distinct client IPs via X-Forwarded-For when exercising partitioned
        // rate limits. Requests that do not set the header behave as before.
        builder.UseSetting("ForwardedHeaders:Enabled", "true");
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(DbContextOptions<MyProjectDbContext>)
                    || descriptor.ServiceType == typeof(DbContextOptions)
                    || descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<MyProjectDbContext>))
                .ToList();
            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<MyProjectDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<IEmailSender>(new CapturingEmailSender(this));

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MyProjectDbContext>();
            context.Database.EnsureCreated();
            var testUser = context.Users.Find(1L);
            if (testUser is null)
            {
                testUser = new User
                {
                    Id = 1,
                    DisplayName = "Test User",
                    Email = "test@example.com",
                    PasswordHash = "test-password-hash"
                };
                context.Users.Add(testUser);
            }

            testUser.DisplayName = "Test User";
            testUser.Email = "test@example.com";
            testUser.PasswordHash = "test-password-hash";
            context.UserRoles.RemoveRange(context.UserRoles.Where(userRole => userRole.UserId == testUser.Id));
            context.UserRoles.Add(new UserRole { UserId = testUser.Id, RoleName = AppRoles.Member });
            context.SaveChanges();
        });
    }

    private sealed class CapturingEmailSender(GeoSceneryApiFactory factory) : IEmailSender
    {
        public Task<EmailDeliveryOutcome> SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken = default)
        {
            factory.LastResetUrl = resetUrl;
            return Task.FromResult(EmailDeliveryOutcome.Sent);
        }

        public Task<EmailDeliveryOutcome> SendEmailVerificationAsync(string recipient, string verificationUrl, CancellationToken cancellationToken = default)
        {
            factory.LastVerificationUrl = verificationUrl;
            if (factory.FailVerificationEmails)
            {
                throw new InvalidOperationException("SMTP unavailable");
            }

            return Task.FromResult(EmailDeliveryOutcome.Sent);
        }

        public Task<EmailDeliveryOutcome> SendContentReportNotificationAsync(string recipient, ContentReportNotification report,
            CancellationToken cancellationToken = default)
        {
            factory.ReportNotifications.Add((recipient, report));
            if (factory.FailReportNotifications)
            {
                throw new InvalidOperationException("SMTP unavailable");
            }

            return Task.FromResult(EmailDeliveryOutcome.Sent);
        }

        public Task<EmailDeliveryOutcome> SendSupportContactAsync(string recipient, SupportContactNotification request,
            CancellationToken cancellationToken = default)
        {
            factory.SupportNotifications.Add((recipient, request));
            return Task.FromResult(EmailDeliveryOutcome.Sent);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
