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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
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
            if (!context.Users.Any())
            {
                context.Users.Add(new User
                {
                    Id = 1,
                    DisplayName = "Test User",
                    Email = "test@example.com",
                    PasswordHash = "test-password-hash"
                });
                context.SaveChanges();
            }
        });
    }

    private sealed class CapturingEmailSender(GeoSceneryApiFactory factory) : IEmailSender
    {
        public Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken cancellationToken = default)
        {
            factory.LastResetUrl = resetUrl;
            return Task.CompletedTask;
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
