using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Infrastructure.Tests.Support;

/// <summary>An in-memory SQLite database that lives as long as this object (the connection stays open).</summary>
public sealed class SqliteDatabase : IDisposable
{
    public SqliteConnection Connection { get; } = new("Data Source=:memory:");

    public SqliteDatabase(bool create = true)
    {
        Connection.Open();
        if (create)
        {
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }
    }

    public DbContextOptions<AppDbContext> Options => new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options;

    public AppDbContext CreateContext() => new(Options);

    public void Dispose() => Connection.Dispose();
}
