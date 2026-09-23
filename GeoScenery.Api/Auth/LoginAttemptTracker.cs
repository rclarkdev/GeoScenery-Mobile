using Microsoft.Extensions.Caching.Memory;

namespace GeoScenery.Api.Auth;

/// <inheritdoc />
public sealed class LoginAttemptTracker(IMemoryCache cache) : ILoginAttemptTracker
{
    /// <summary>Number of failed attempts before an account is considered locked.</summary>
    private const int MaxFailedAttempts = 10;

    /// <summary>Sliding window during which failures accumulate.</summary>
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    public bool IsLocked(string email)
    {
        return TryGetCount(email, out var count) && count >= MaxFailedAttempts;
    }

    public void RecordFailure(string email)
    {
        var count = TryGetCount(email, out var current) ? current + 1 : 1;
        cache.Set(
            GetKey(email),
            count,
            new MemoryCacheEntryOptions().SetSlidingExpiration(LockoutWindow));
    }

    public void Reset(string email)
    {
        cache.Remove(GetKey(email));
    }

    private static string GetKey(string email) => "auth:login-failures:" + email.Trim().ToLowerInvariant();

    private bool TryGetCount(string email, out int count)
    {
        if (cache.TryGetValue(GetKey(email), out var value) && value is int existing)
        {
            count = existing;
            return true;
        }

        count = 0;
        return false;
    }
}