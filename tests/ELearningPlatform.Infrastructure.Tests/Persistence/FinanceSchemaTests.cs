using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Infrastructure.Tests.Persistence;

/// <summary>Database-level guarantees of the payments schema, on a migrated database.</summary>
public class FinanceSchemaTests
{
    private static async Task<(SqliteDatabase Db, Guid TransactionId)> MigratedWithLedgerRowAsync()
    {
        var db = new SqliteDatabase(create: false);
        await using var context = db.CreateContext();
        await context.Database.MigrateAsync();

        var now = DateTime.UtcNow;
        var a = Wallet.System(WalletType.ExternalClearing, "USD", now);
        var b = Wallet.System(WalletType.PlatformRevenue, "USD", now);
        var transaction = new LedgerTransaction { Type = LedgerTransactionType.TopUp, Currency = "USD", Description = "t", CreatedAt = now };
        transaction.Entries.Add(new LedgerEntry { Wallet = a, Direction = EntryDirection.Debit, Amount = 1m, Currency = "USD", BalanceAfter = a.Apply(EntryDirection.Debit, 1m, now), CreatedAt = now });
        transaction.Entries.Add(new LedgerEntry { Wallet = b, Direction = EntryDirection.Credit, Amount = 1m, Currency = "USD", BalanceAfter = b.Apply(EntryDirection.Credit, 1m, now), CreatedAt = now });
        context.LedgerTransactions.Add(transaction);
        await context.SaveChangesAsync();
        return (db, transaction.Id);
    }

    [Theory]
    [InlineData("UPDATE LedgerEntries SET Amount = '5'")]
    [InlineData("DELETE FROM LedgerEntries")]
    [InlineData("UPDATE LedgerTransactions SET Description = 'x'")]
    [InlineData("DELETE FROM LedgerTransactions")]
    public async Task Triggers_reject_changes_to_the_ledger_even_with_raw_sql(string sql)
    {
        var (db, _) = await MigratedWithLedgerRowAsync();
        using (db)
        {
            await using var context = db.CreateContext();

            var ex = await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(sql));

            Assert.Contains("append-only", ex.Message);
        }
    }

    [Fact]
    public async Task Triggers_reject_changes_to_the_audit_log()
    {
        var (db, _) = await MigratedWithLedgerRowAsync();
        using (db)
        {
            await using var context = db.CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO AuditLogs (Action, EntityType, EntityId, Timestamp) VALUES ('Added', 'Wallet', '1', '2026-01-01')");

            await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs"));
        }
    }

    [Fact]
    public async Task Only_one_wallet_per_user_and_currency()
    {
        using var db = new SqliteDatabase(create: false);
        await using var context = db.CreateContext();
        await context.Database.MigrateAsync();
        var user = new ApplicationUser { Id = "u", UserName = "u", Email = "u@x.test" };
        context.Users.Add(user);
        context.Wallets.Add(Wallet.ForUser("u", "USD", DateTime.UtcNow));
        await context.SaveChangesAsync();

        context.Wallets.Add(Wallet.ForUser("u", "USD", DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Money_round_trips_exactly()
    {
        var (db, transactionId) = await MigratedWithLedgerRowAsync();
        using (db)
        {
            await using var context = db.CreateContext();
            var payment = new Payment { UserId = "u", Amount = 1234567.89m, Currency = "USD", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            context.Users.Add(new ApplicationUser { Id = "u", UserName = "u" });
            payment.WalletId = (await context.Wallets.FirstAsync()).Id;
            context.Payments.Add(payment);
            await context.SaveChangesAsync();

            await using var read = db.CreateContext();
            Assert.Equal(1234567.89m, (await read.Payments.SingleAsync()).Amount);
            Assert.Equal(PaymentStatus.Pending, (await read.Payments.SingleAsync()).Status);
            Assert.Equal(2, await read.LedgerEntries.CountAsync(e => e.TransactionId == transactionId));
        }
    }
}
