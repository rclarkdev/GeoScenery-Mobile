using GeoScenery.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GeoScenery.Tests;

public sealed class GeoSceneryApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
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

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<MyProjectDbContext>().Database.EnsureCreated();
        });
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
