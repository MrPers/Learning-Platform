using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Services.Payments;

public interface IWalletService
{
    Task<Result<WalletDto>> GetMyWalletAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<StatementLineDto>>> GetStatementAsync(string userId, StatementQuery query, string baseUrl, CancellationToken cancellationToken = default);
    Task<Result<CsvFileDto>> ExportStatementCsvAsync(string userId, StatementQuery query, CancellationToken cancellationToken = default);
    /// <summary>Checks the ledger: cached balances match entries, every transaction balances, all balances sum to zero.</summary>
    Task<Result<ReconciliationDto>> ReconcileAsync(CancellationToken cancellationToken = default);
}

public sealed class WalletService(IAppDbContext db, Ledger ledger, FinanceTransaction finance, TimeProvider timeProvider) : IWalletService
{
    /// <summary>Upper bound on rows in one CSV export.</summary>
    public const int MaxExportRows = 50_000;

    public async Task<Result<WalletDto>> GetMyWalletAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (await ledger.FindUserWalletAsync(userId, cancellationToken) is { } existing)
            return Result<WalletDto>.Success(ToDto(existing));

        return await finance.ExecuteAsync(async ct =>
        {
            var wallet = await ledger.GetOrCreateUserWalletAsync(userId, ct);
            return Result<WalletDto>.Success(ToDto(wallet));
        }, cancellationToken);
    }

    public async Task<Result<PaginatedResult<StatementLineDto>>> GetStatementAsync(string userId, StatementQuery query, string baseUrl, CancellationToken cancellationToken = default)
    {
        var page = await StatementLines(userId, query)
            .ToPaginatedResultAsync(query.PageNumber, query.PageSize, baseUrl, cancellationToken);
        return Result<PaginatedResult<StatementLineDto>>.Success(page);
    }

    public async Task<Result<CsvFileDto>> ExportStatementCsvAsync(string userId, StatementQuery query, CancellationToken cancellationToken = default)
    {
        var lines = await StatementLines(userId, query).Take(MaxExportRows).ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Date (UTC),Transaction,Type,Direction,Amount,Currency,Balance after,Description,Reference");
        foreach (var line in lines)
        {
            csv.AppendJoin(',',
                Csv.Field(line.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Csv.Field(line.TransactionId.ToString()),
                Csv.Field(line.Type.ToString()),
                Csv.Field(line.Direction.ToString()),
                Csv.Field((line.Direction == EntryDirection.Credit ? line.Amount : -line.Amount).ToString("0.00", CultureInfo.InvariantCulture)),
                Csv.Field(line.Currency),
                Csv.Field(line.BalanceAfter.ToString("0.00", CultureInfo.InvariantCulture)),
                Csv.Field(line.Description),
                Csv.Field(line.Reference));
            csv.AppendLine();
        }

        // BOM so Excel opens it as UTF-8
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var fileName = $"statement-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.csv";
        return Result<CsvFileDto>.Success(new CsvFileDto(content, fileName));
    }

    public async Task<Result<ReconciliationDto>> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        // Sums run in memory: SQLite has no native decimal type, and exactness matters more than speed here
        var wallets = await db.Wallets.AsNoTracking()
            .Select(w => new { w.Id, w.Type, w.OwnerUserId, w.Balance }).ToListAsync(cancellationToken);
        var entries = await db.LedgerEntries.AsNoTracking()
            .Select(e => new { e.WalletId, e.TransactionId, e.Direction, e.Amount }).ToListAsync(cancellationToken);

        var ledgerBalances = entries.GroupBy(e => e.WalletId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Direction == EntryDirection.Credit ? e.Amount : -e.Amount));

        var mismatches = wallets
            .Select(w => new WalletMismatchDto(w.Id, w.Type, w.OwnerUserId, w.Balance, ledgerBalances.GetValueOrDefault(w.Id)))
            .Where(m => m.CachedBalance != m.LedgerBalance)
            .ToList();

        var unbalanced = entries.GroupBy(e => e.TransactionId)
            .Where(g => g.Sum(e => e.Direction == EntryDirection.Credit ? e.Amount : -e.Amount) != 0)
            .Select(g => g.Key)
            .ToList();

        var total = ledgerBalances.Values.Sum();
        var transactionCount = entries.Select(e => e.TransactionId).Distinct().Count();
        var dto = new ReconciliationDto(mismatches.Count == 0 && unbalanced.Count == 0 && total == 0, total,
            wallets.Count, transactionCount, mismatches, unbalanced);
        return Result<ReconciliationDto>.Success(dto);
    }

    private IQueryable<StatementLineDto> StatementLines(string userId, StatementQuery query)
    {
        var entries = db.LedgerEntries.AsNoTracking()
            .Where(e => e.Wallet.Type == WalletType.User && e.Wallet.OwnerUserId == userId);

        if (query.From is { } from)
            entries = entries.Where(e => e.CreatedAt >= from);
        if (query.To is { } to)
            entries = entries.Where(e => e.CreatedAt < to);
        if (query.Type is { } type)
            entries = entries.Where(e => e.Transaction.Type == type);
        if (query.Direction is { } direction)
            entries = entries.Where(e => e.Direction == direction);

        return entries.OrderByDescending(e => e.Id).Select(PaymentMappings.EntryToStatementLine);
    }

    private static WalletDto ToDto(Wallet wallet) => new(wallet.Id, wallet.Balance, wallet.Currency, wallet.UpdatedAt);
}

/// <summary>RFC 4180 fields, with spreadsheet formula injection neutralised.</summary>
public static class Csv
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // A cell starting with = + - @ is run as a formula by Excel; prefix with ' unless it's a plain number
        if (FormulaPrefixes.Contains(value[0]) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            value = "'" + value;

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
