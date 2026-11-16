using System.Text;

namespace ELearningPlatform.Application.Tests.Services.Payments;

public sealed class WalletServiceTests : IAsyncLifetime
{
    private const string BaseUrl = "https://api.test/api/Wallet/statement";
    private ServiceTestContext _ctx = null!;
    private IWalletService Sut => _ctx.Get<IWalletService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    /// <summary>Student funded with 100 at T0, buys a 30 course at T0+1h and a 20 course at T0+2h.</summary>
    private async Task<Student> StudentWithHistoryAsync()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var a = await _ctx.CreateCourseAsync(teacher, "Course A", price: 30m);
        var b = await _ctx.CreateCourseAsync(teacher, "=HYPERLINK(\"x\")", price: 20m);
        var student = await _ctx.CreateStudentAsync();
        await _ctx.FundWalletAsync(student.ApplicationUserId, 100m);
        _ctx.Time.Advance(TimeSpan.FromHours(1));
        await _ctx.Get<IPurchaseService>().PurchaseAsync(student.ApplicationUserId, a.Id);
        _ctx.Time.Advance(TimeSpan.FromHours(1));
        await _ctx.Get<IPurchaseService>().PurchaseAsync(student.ApplicationUserId, b.Id);
        return student;
    }

    [Fact]
    public async Task Wallet_is_created_on_first_access()
    {
        var student = await _ctx.CreateStudentAsync();

        var first = await Sut.GetMyWalletAsync(student.ApplicationUserId);
        var second = await Sut.GetMyWalletAsync(student.ApplicationUserId);

        Assert.True(first.IsSuccess, first.Message);
        Assert.Equal((0m, "USD"), (first.Data!.Balance, first.Data.Currency));
        Assert.Equal(first.Data.Id, second.Data!.Id);
        Assert.Equal(1, await _ctx.Db.Wallets.CountAsync());
    }

    [Fact]
    public async Task Statement_lists_entries_newest_first_with_running_balance()
    {
        var student = await StudentWithHistoryAsync();

        var page = (await Sut.GetStatementAsync(student.ApplicationUserId, new StatementQuery(), BaseUrl)).Data!;

        Assert.Equal(3, page.TotalCount);
        Assert.Equal([50m, 70m, 100m], page.Items.Select(i => i.BalanceAfter));
        Assert.Equal([LedgerTransactionType.CoursePurchase, LedgerTransactionType.CoursePurchase, LedgerTransactionType.TopUp], page.Items.Select(i => i.Type));
        Assert.Equal(EntryDirection.Debit, page.Items[0].Direction);
        Assert.StartsWith("Course purchase:", page.Items[0].Description);
    }

    [Fact]
    public async Task Statement_filters_and_paginates_keeping_filters_in_links()
    {
        var student = await StudentWithHistoryAsync();

        var debits = (await Sut.GetStatementAsync(student.ApplicationUserId,
            new StatementQuery { Direction = EntryDirection.Debit, PageSize = 1 }, BaseUrl + "?Direction=Debit")).Data!;
        var lastHour = (await Sut.GetStatementAsync(student.ApplicationUserId,
            new StatementQuery { From = _ctx.Now.AddMinutes(-30) }, BaseUrl)).Data!;
        var topUps = (await Sut.GetStatementAsync(student.ApplicationUserId,
            new StatementQuery { Type = LedgerTransactionType.TopUp }, BaseUrl)).Data!;

        Assert.Equal((2, 2), (debits.TotalCount, debits.TotalPages));
        Assert.Equal(BaseUrl + "?Direction=Debit&pageNumber=2&pageSize=1", debits.NextPage);
        Assert.Equal(20m, Assert.Single(lastHour.Items).Amount);
        Assert.Equal(100m, Assert.Single(topUps.Items).Amount);
    }

    [Fact]
    public async Task Statement_shows_only_my_wallet()
    {
        await StudentWithHistoryAsync();
        var other = await _ctx.CreateStudentAsync();

        var page = (await Sut.GetStatementAsync(other.ApplicationUserId, new StatementQuery(), BaseUrl)).Data!;

        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Csv_export_has_signed_amounts_and_neutralises_formulas()
    {
        var student = await StudentWithHistoryAsync();

        var file = (await Sut.ExportStatementCsvAsync(student.ApplicationUserId, new StatementQuery())).Data!;

        Assert.EndsWith(".csv", file.FileName);
        Assert.Equal(Encoding.UTF8.GetPreamble(), file.Content[..3]);
        var lines = Encoding.UTF8.GetString(file.Content[3..]).TrimEnd().Split(Environment.NewLine);
        Assert.Equal(4, lines.Length);
        Assert.StartsWith("Date (UTC),Transaction,Type,Direction,Amount", lines[0]);
        Assert.Contains(",CoursePurchase,Debit,-20.00,USD,50.00,", lines[1]);
        // Quoted, with inner quotes doubled
        Assert.Contains(",\"Course purchase: =HYPERLINK(\"\"x\"\")\",", lines[1]);
        Assert.Contains(",TopUp,Credit,100.00,USD,100.00,", lines[3]);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("-12.50", "-12.50")]
    [InlineData(null, "")]
    public void Csv_field_escaping(string? value, string expected) => Assert.Equal(expected, Csv.Field(value));

    [Fact]
    public async Task Reconciliation_reports_a_consistent_ledger()
    {
        await StudentWithHistoryAsync();

        var report = (await Sut.ReconcileAsync()).Data!;

        Assert.True(report.IsConsistent);
        Assert.Equal(0m, report.LedgerTotal);
        Assert.Equal(3, report.TransactionCount);
        Assert.Empty(report.Mismatches);
    }

    [Fact]
    public async Task Reconciliation_detects_a_tampered_cached_balance()
    {
        var student = await StudentWithHistoryAsync();
        await _ctx.Db.Database.ExecuteSqlRawAsync("UPDATE Wallets SET Balance = '1000' WHERE OwnerUserId = {0}", student.ApplicationUserId);

        var report = (await Sut.ReconcileAsync()).Data!;

        Assert.False(report.IsConsistent);
        var mismatch = Assert.Single(report.Mismatches);
        Assert.Equal((1000m, 50m), (mismatch.CachedBalance, mismatch.LedgerBalance));
    }
}
