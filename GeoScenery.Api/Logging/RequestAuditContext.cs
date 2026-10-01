namespace GeoScenery.Api.Logging;

/// <summary>Small, non-sensitive operation details folded into the request's single audit row.</summary>
public static class RequestAuditContext
{
    private const string PropertiesItemKey = "GeoScenery.AuditProperties";

    public static void Set(HttpContext context, string name, object? value)
    {
        if (context.Items[PropertiesItemKey] is not Dictionary<string, object?> properties)
        {
            properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            context.Items[PropertiesItemKey] = properties;
        }

        properties[name] = value;
    }

    public static IReadOnlyDictionary<string, object?>? Get(HttpContext context) =>
        context.Items[PropertiesItemKey] as IReadOnlyDictionary<string, object?>;

    public static string? GetOperationOutcome(HttpContext context) =>
        Get(context) is { } properties
        && properties.TryGetValue("operationOutcome", out var outcome)
        && outcome is string value
            ? value
            : null;
}
