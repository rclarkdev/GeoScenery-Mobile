using Microsoft.AspNetCore.Http;

namespace GeoScenery.Api.Logging;

public interface ISqlAuditLog
{
    Task WriteAsync(
        HttpContext? httpContext,
        string level,
        string eventName,
        string message,
        long? userId = null,
        IReadOnlyDictionary<string, object?>? properties = null,
        CancellationToken cancellationToken = default);
}