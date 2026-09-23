using Microsoft.Extensions.Configuration;
using System.IO;

namespace GeoScenery.Api.Storage;

/// <summary>Resolves where uploaded files are stored.</summary>
public static class StoragePaths
{
    /// <summary>
    /// Returns the configured Storage:Local:RootPath when present, otherwise the
    /// output directory of the running application. Production deployments should
    /// point this at a persistent path (see appsettings.Production.template.json).
    /// </summary>
    public static string ResolveRootPath(IConfiguration configuration)
    {
        var configured = configuration["Storage:Local:RootPath"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "uploads")
            : configured;
    }
}