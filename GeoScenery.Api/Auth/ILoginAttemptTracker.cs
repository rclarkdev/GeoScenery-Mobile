namespace GeoScenery.Api.Auth;

/// <summary>
/// Tracks repeated failed password attempts per account so a single user (or a
/// distributed attacker) cannot keep guessing a password indefinitely.
///
/// The tracker is intentionally in-memory and process-local: it complements the
/// per-IP HTTP rate limiters and is safe for the single-instance deployments this
/// application targets. It must be re-evaluated if the API is ever run on multiple
/// instances (see deployment notes).
/// </summary>
public interface ILoginAttemptTracker
{
    /// <summary>Returns true when the account has exceeded the allowed failed-attempt budget.</summary>
    bool IsLocked(string email);

    /// <summary>Records one failed login attempt for the account.</summary>
    void RecordFailure(string email);

    /// <summary>Clears the failure budget, e.g. after a successful login.</summary>
    void Reset(string email);
}