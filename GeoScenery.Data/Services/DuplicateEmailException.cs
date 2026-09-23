namespace GeoScenery.Data.Services;

/// <summary>
/// Thrown when a user operation would create or update an account whose email is
/// already registered to another account. Callers are expected to translate this
/// into a 409 Conflict response.
/// </summary>
public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException() : base("An account with this email already exists.")
    {
    }
}