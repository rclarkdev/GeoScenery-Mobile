using GeoScenery.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace GeoScenery.Data.Context;

public sealed class MigrationMyProjectDbContextFactory : IDesignTimeDbContextFactory<MyProjectDbContext>
{
    public MyProjectDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var options = new DbContextOptionsBuilder<MyProjectDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new MyProjectDbContext(options);
    }
}
