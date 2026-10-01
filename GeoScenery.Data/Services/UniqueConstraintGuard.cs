using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Data.Services;

/// <summary>
/// Detects genuine unique-constraint violations inside an EF Core
/// <see cref="DbUpdateException"/>. Only known "duplicate key" failures are
/// recognized; every other database failure is left for the caller to treat as a
/// server error. Provider-specific codes are intentionally narrow:
///
/// SQL Server: 2601 (duplicate key in unique index), 2627 (unique constraint).
/// SQLite:     SQLITE_CONSTRAINT (19) with extended code 2067
///             (SQLITE_CONSTRAINT_UNIQUE), as used by the test database. SQLite
///             is identified by exception type name to avoid shipping its native
///             test provider as a production dependency.
/// </summary>
public static class UniqueConstraintGuard
{
    public const int SqlServerDuplicateKeyError = 2601;
    public const int SqlServerUniqueConstraintError = 2627;
    public const int SqliteConstraintError = 19;
    public const int SqliteUniqueExtendedError = 2067;

    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception is null)
        {
            return false;
        }

        for (var current = (Exception)exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case SqlException sql when sql.Number is SqlServerDuplicateKeyError or SqlServerUniqueConstraintError:
                    return true;

            }

            var exceptionType = current.GetType();
            if (string.Equals(exceptionType.FullName, "Microsoft.Data.Sqlite.SqliteException", StringComparison.Ordinal)
                && exceptionType.GetProperty("SqliteErrorCode")?.GetValue(current) is int sqliteErrorCode
                && sqliteErrorCode == SqliteConstraintError
                && exceptionType.GetProperty("SqliteExtendedErrorCode")?.GetValue(current) is int sqliteExtendedErrorCode
                && sqliteExtendedErrorCode == SqliteUniqueExtendedError)
            {
                return true;
            }
        }

        return false;
    }
}