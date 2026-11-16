using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ELearningPlatform.Infrastructure.Tests.Persistence;

public class MigrationsTests
{
    [Fact]
    public async Task Migrate_AppliesAllMigrationsOnFreshDatabase()
    {
        using var db = new SqliteDatabase(create: false);
        await using var context = db.CreateContext();

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public void Model_HasNoChangesMissingFromMigrations()
    {
        using var db = new SqliteDatabase(create: false);
        using var context = db.CreateContext();

        Assert.False(context.Database.HasPendingModelChanges(), "The model changed without a migration: run 'dotnet ef migrations add'.");
    }

    [Fact]
    public void Migrations_IncludeHashedRefreshTokenMigration()
    {
        using var db = new SqliteDatabase(create: false);
        using var context = db.CreateContext();

        Assert.Contains(context.Database.GetMigrations(), m => m.EndsWith("_HashedRefreshTokensAndDecimalPrice"));
    }

    [Fact]
    public async Task Migrated_DatabaseAcceptsEntities()
    {
        using var db = new SqliteDatabase(create: false);
        await using (var context = db.CreateContext())
        {
            await context.Database.MigrateAsync();
            var user = new ApplicationUser { Id = "u", UserName = "u", Email = "u@x.test" };
            context.Teachers.Add(new Teacher { User = user, Specialization = Specialization.Biology });
            context.RefreshTokens.Add(new RefreshToken { ApplicationUser = user, TokenHash = new string('A', 64), CreatedAt = DateTime.UtcNow, ExpiresOn = DateTime.UtcNow });
            await context.SaveChangesAsync();
        }

        await using var read = db.CreateContext();
        Assert.Equal(new string('A', 64), (await read.RefreshTokens.SingleAsync()).TokenHash);
    }

    [Fact]
    public async Task HashedRefreshTokenMigration_DropsExistingPlainTextTokens()
    {
        using var db = new SqliteDatabase(create: false);
        await using var context = db.CreateContext();
        var migrator = context.GetService<IMigrator>();
        var migrations = context.Database.GetMigrations().ToList();
        var target = migrations.Single(m => m.EndsWith("_HashedRefreshTokensAndDecimalPrice"));
        var previous = migrations[migrations.IndexOf(target) - 1];

        await migrator.MigrateAsync(previous);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO AspNetUsers (Id, UserName, FirstName, LastName, Gender, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount) VALUES ('u','u','f','l',1,0,0,0,0,0);");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO RefreshTokens (Token, CreatedAt, ExpiresOn, ApplicationUserId) VALUES ('plain-1','2026-01-01','2026-02-01','u'), ('plain-2','2026-01-01','2026-02-01','u');");

        await migrator.MigrateAsync(target);

        Assert.Equal(0, await context.RefreshTokens.CountAsync());
    }
}
