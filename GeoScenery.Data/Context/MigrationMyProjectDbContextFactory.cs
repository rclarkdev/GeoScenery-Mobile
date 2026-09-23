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
        var configuration = CreateConfiguration();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var options = new DbContextOptionsBuilder<MyProjectDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new MyProjectDbContext(options);
    }

    /// <summary>
    /// Builds the configuration used to connect at design time, following the same
    /// hierarchy as the running application:
    /// appsettings.json → appsettings.{Environment}.json → environment variables.
    ///
    /// This ensures `dotnet ef database update` honors values such as
    /// ConnectionStrings__DefaultConnection that GitHub Actions / App Service pass
    /// through environment variables, instead of always falling back to the
    /// appsettings.json LocalDb value from source control.
    /// </summary>
    /// <remarks>
    /// The base path is the current directory when it contains appsettings.json
    /// (local developer runs from the project folder). Otherwise the assembly
    /// output directory is used, which is where `dotnet ef` copies the content
    /// files when it is executed from a different working directory (for example
    /// from the repository root in CI).
    /// </remarks>
    public static IConfiguration CreateConfiguration(
        string? basePath = null,
        string? environment = null)
    {
        var resolvedBasePath = basePath ?? ResolveBasePath();
        var resolvedEnvironment = environment
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        var builder = new ConfigurationBuilder()
            .SetBasePath(resolvedBasePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{resolvedEnvironment}.json", optional: true);
        builder.AddEnvironmentVariables();

        return builder.Build();
    }

    private static string ResolveBasePath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(currentDirectory, "appsettings.json")))
        {
            return currentDirectory;
        }

        return AppContext.BaseDirectory;
    }
}
