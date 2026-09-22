using GeoScenery.Data.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeoScenery.Tests;

public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        Context = new MyProjectDbContext(new DbContextOptionsBuilder<MyProjectDbContext>()
            .UseSqlite(_connection)
            .Options);
        Context.Database.EnsureCreated();
    }

    public MyProjectDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
