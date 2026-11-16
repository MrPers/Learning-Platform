namespace ELearningPlatform.Application.Tests.Services.Payments;

public sealed class LedgerTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private Ledger Ledger => _ctx.Get<Ledger>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Posting_writes_a_debit_and_a_credit_and_updates_cached_balances()
    {
        var student = await _ctx.CreateStudentAsync();

        await _ctx.FundWalletAsync(student.ApplicationUserId, 50m);

        var entries = await _ctx.Db.LedgerEntries.AsNoTracking().Include(e => e.Wallet).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Single(entries, e => e.Direction == EntryDirection.Debit && e.Wallet.Type == WalletType.ExternalClearing && e.Amount == 50m);
        Assert.Single(entries, e => e.Direction == EntryDirection.Credit && e.Wallet.OwnerUserId == student.ApplicationUserId && e.BalanceAfter == 50m);
        Assert.Equal(50m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.Equal(-50m, await _ctx.SystemBalanceAsync(WalletType.ExternalClearing));
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task Unbalanced_transaction_is_rejected()
    {
        var wallet = Wallet.ForUser("u", "USD", _ctx.Now);
        var clearing = Wallet.System(WalletType.ExternalClearing, "USD", _ctx.Now);

        var ex = Assert.Throws<DomainException>(() => Ledger.Post(LedgerTransactionType.TopUp, "x", null, null,
            [new(clearing, EntryDirection.Debit, 10m), new(wallet, EntryDirection.Credit, 9.99m)]));

        Assert.Equal(DomainErrors.Unbalanced, ex.Code);
        Assert.Equal(0m, wallet.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.005)]
    public void Invalid_amount_is_rejected(double amount)
    {
        var a = Wallet.System(WalletType.ExternalClearing, "USD", _ctx.Now);
        var b = Wallet.System(WalletType.PlatformRevenue, "USD", _ctx.Now);

        var ex = Assert.Throws<DomainException>(() => Ledger.Post(LedgerTransactionType.TopUp, "x", null, null,
            [new(a, EntryDirection.Debit, (decimal)amount), new(b, EntryDirection.Credit, (decimal)amount)]));

        Assert.Equal(DomainErrors.InvalidAmount, ex.Code);
    }

    [Fact]
    public void Other_currency_is_rejected()
    {
        var eur = Wallet.ForUser("u", "EUR", _ctx.Now);
        var clearing = Wallet.System(WalletType.ExternalClearing, "USD", _ctx.Now);

        var ex = Assert.Throws<DomainException>(() => Ledger.Post(LedgerTransactionType.TopUp, "x", null, null,
            [new(clearing, EntryDirection.Debit, 1m), new(eur, EntryDirection.Credit, 1m)]));

        Assert.Equal(DomainErrors.CurrencyMismatch, ex.Code);
    }

    [Fact]
    public void User_wallet_cannot_go_negative_but_system_wallets_can()
    {
        var user = Wallet.ForUser("u", "USD", _ctx.Now);
        var clearing = Wallet.System(WalletType.ExternalClearing, "USD", _ctx.Now);

        Assert.Equal(-1m, clearing.Apply(EntryDirection.Debit, 1m, _ctx.Now));
        var ex = Assert.Throws<DomainException>(() => user.Apply(EntryDirection.Debit, 1m, _ctx.Now));
        Assert.Equal(DomainErrors.InsufficientFunds, ex.Code);
        Assert.Equal(-1m, user.Apply(EntryDirection.Debit, 1m, _ctx.Now, allowOverdraft: true));
    }

    [Fact]
    public void Every_balance_change_bumps_the_concurrency_token()
    {
        var wallet = Wallet.ForUser("u", "USD", _ctx.Now);

        wallet.Apply(EntryDirection.Credit, 5m, _ctx.Now);
        wallet.Apply(EntryDirection.Debit, 2m, _ctx.Now);

        Assert.Equal(2, wallet.RowVersion);
        Assert.Equal(3m, wallet.Balance);
    }

    [Fact]
    public async Task Reversal_mirrors_the_original_and_keeps_both()
    {
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 20m);
        var original = await _ctx.Db.LedgerTransactions.AsNoTracking().SingleAsync();

        var result = await _ctx.Get<FinanceTransaction>().ExecuteAsync(async ct =>
            Result<LedgerTransaction>.Success(await Ledger.ReverseAsync(original.Id, "undo", null, cancellationToken: ct)));

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, await _ctx.Db.LedgerTransactions.CountAsync());
        Assert.Equal(4, await _ctx.Db.LedgerEntries.CountAsync());
        Assert.Equal(original.Id, result.Data!.ReversalOfId);
        Assert.Equal(0m, await _ctx.BalanceAsync(student.ApplicationUserId));
        Assert.True(await Ledger.IsReversedAsync(original.Id));
        await _ctx.AssertLedgerConsistentAsync();
    }

    [Fact]
    public async Task A_transaction_cannot_be_reversed_twice()
    {
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 20m);
        var original = await _ctx.Db.LedgerTransactions.AsNoTracking().SingleAsync();
        var finance = _ctx.Get<FinanceTransaction>();
        await finance.ExecuteAsync(async ct => Result<LedgerTransaction>.Success(await Ledger.ReverseAsync(original.Id, "undo", null, cancellationToken: ct)));

        await Assert.ThrowsAsync<DomainException>(() => finance.ExecuteAsync(async ct =>
            Result<LedgerTransaction>.Success(await Ledger.ReverseAsync(original.Id, "again", null, cancellationToken: ct))));
    }

    [Fact]
    public async Task Ledger_rows_are_append_only()
    {
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 20m);

        var entry = await _ctx.Db.LedgerEntries.FirstAsync();
        entry.Amount = 1m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _ctx.Db.SaveChangesAsync());
        _ctx.Db.ChangeTracker.Clear();

        _ctx.Db.LedgerTransactions.Remove(await _ctx.Db.LedgerTransactions.FirstAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _ctx.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Stale_wallet_write_fails_with_a_concurrency_conflict()
    {
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 20m);
        var walletId = (await _ctx.Db.Wallets.AsNoTracking().SingleAsync(w => w.OwnerUserId == student.ApplicationUserId)).Id;

        // Two contexts read the same balance; the second write must not overwrite the first
        var ex = await _ctx.InNewScopeAsync(async first => await _ctx.InNewScopeAsync(async second =>
        {
            var a = await first.GetRequiredService<AppDbContext>().Wallets.SingleAsync(w => w.Id == walletId);
            var b = await second.GetRequiredService<AppDbContext>().Wallets.SingleAsync(w => w.Id == walletId);
            a.Apply(EntryDirection.Debit, 15m, _ctx.Now);
            b.Apply(EntryDirection.Debit, 15m, _ctx.Now);
            await first.GetRequiredService<AppDbContext>().SaveChangesAsync();
            return await Record.ExceptionAsync(() => second.GetRequiredService<AppDbContext>().SaveChangesAsync());
        }));

        Assert.IsType<DbUpdateConcurrencyException>(ex);
        Assert.Equal(5m, await _ctx.BalanceAsync(student.ApplicationUserId));
    }

    [Theory]
    [InlineData(100, 20, 20)]
    [InlineData(49.99, 20, 10)]
    [InlineData(0.01, 20, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 100, 10)]
    [InlineData(33.33, 15, 5)]
    public void Platform_fee_is_rounded_to_cents(double price, double percent, double expectedFee)
        => Assert.Equal((decimal)expectedFee, PurchaseService.PlatformFee((decimal)price, (decimal)percent));
}
