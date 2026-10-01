using GeoScenery.Data.Context;
using GeoScenery.Data.Models;
using GeoScenery.Data.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Tests;

/// <summary>
/// Opt-in tests for SQL Server locking/index behavior; set GEOSCENERY_SQLSERVER_TEST_CONNECTION
/// to a SQL Server/LocalDB connection with permission to create and drop a disposable database.
/// </summary>
[TestFixture]
public sealed class SqlServerAdminConcurrencyTests
{
    private string _databaseName = string.Empty;
    private string _databaseConnectionString = string.Empty;
    private string _masterConnectionString = string.Empty;
    private long _secondAdminId;

    [SetUp]
    public async Task SetUp()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("GEOSCENERY_SQLSERVER_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            Assert.Ignore("Set GEOSCENERY_SQLSERVER_TEST_CONNECTION to enable SQL Server concurrency tests.");
        }

        var builder = new SqlConnectionStringBuilder(configuredConnection);
        _databaseName = $"GeoSceneryAdminTests_{Guid.NewGuid():N}";
        var master = new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" };
        _masterConnectionString = master.ConnectionString;
        builder.InitialCatalog = _databaseName;
        _databaseConnectionString = builder.ConnectionString;

        await using (var connection = new SqlConnection(_masterConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Users.Add(new User
        {
            DisplayName = "Concurrent administrator",
            Email = "concurrent-admin@example.com",
            PasswordHash = "test-hash",
            IsEmailVerified = true,
            Roles = [new UserRole { RoleName = AppRoles.Admin }]
        });
        await db.SaveChangesAsync();
        _secondAdminId = await db.Users
            .Where(user => user.Email == "concurrent-admin@example.com")
            .Select(user => user.Id)
            .SingleAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (string.IsNullOrWhiteSpace(_masterConnectionString) || string.IsNullOrWhiteSpace(_databaseName))
        {
            return;
        }

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    [Test]
    public async Task ConcurrentLastAdminDeletions_CannotRemoveEveryAdmin()
    {
        var first = Task.Run(async () =>
        {
            await using var db = CreateContext();
            return await TryDeleteAdminAsync(db, 1);
        });
        var second = Task.Run(async () =>
        {
            await using var db = CreateContext();
            return await TryDeleteAdminAsync(db, _secondAdminId);
        });

        var results = await Task.WhenAll(first, second);
        await using var verify = CreateContext();
        var activeAdminCount = await verify.UserRoles.CountAsync(role =>
            role.RoleName == AppRoles.Admin && !role.User.IsSuspended);

        Assert.That(results.Count(result => result), Is.LessThanOrEqualTo(1));
        Assert.That(activeAdminCount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task ConcurrentDuplicateOpenReports_AreLimitedByTheFilteredUniqueIndex()
    {
        async Task<bool> TryInsertAsync()
        {
            await using var db = CreateContext();
            db.ContentReports.Add(new ContentReport
            {
                ReporterId = 1,
                ReporterDisplayName = "Master",
                ReporterEmail = "master@example.com",
                TargetType = ContentReportTargets.Profile,
                TargetId = 555,
                TargetLabel = "Target profile",
                Description = "Concurrent report",
                Status = ContentReportStatuses.Pending
            });
            try
            {
                await db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(Task.Run(TryInsertAsync), Task.Run(TryInsertAsync));
        await using var verify = CreateContext();
        var matchingReports = await verify.ContentReports.CountAsync(report => report.ReporterId == 1
            && report.TargetType == ContentReportTargets.Profile && report.TargetId == 555
            && (report.Status == ContentReportStatuses.Pending || report.Status == ContentReportStatuses.Reviewed));

        Assert.That(results.Count(success => success), Is.EqualTo(1));
        Assert.That(matchingReports, Is.EqualTo(1));
    }

    private MyProjectDbContext CreateContext() => new(new DbContextOptionsBuilder<MyProjectDbContext>()
        .UseSqlServer(_databaseConnectionString)
        .Options);

    private static async Task<bool> TryDeleteAdminAsync(MyProjectDbContext db, long id)
    {
        try
        {
            return await new UserService(db).DeleteAsync(id);
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            // SQL Server may choose a deadlock victim; that transaction rolls back,
            // preserving at least one active administrator.
            return false;
        }
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
    }
}
