namespace GeoScenery.Api.Auth;

public sealed record SupportContactNotification(
    string Name,
    string Email,
    string Topic,
    string Message,
    DateTimeOffset SubmittedAt);

/// <summary>Result returned after a support message is delivered to administrators.</summary>
public sealed record SupportContactResponse(string Message);
