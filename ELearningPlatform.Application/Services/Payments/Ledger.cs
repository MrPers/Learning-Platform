using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Services.Payments;

/// <summary>One side of a ledger transaction.</summary>
public sealed record Posting(Wallet Wallet, EntryDirection Direction, decimal Amount);

/// <summary>
/// Double-entry bookkeeping. Every movement of money is a <see cref="LedgerTransaction"/> whose debits equal
/// its credits, so the sum of all wallet balances is always zero. Entries are only ever appended; wallets'
/// cached balances are updated in the same unit of work. Callers run inside <see cref="FinanceTransaction"/>.
/// </summary>
public sealed class Ledger(IAppDbContext db, TimeProvider timeProvider, IOptions<PaymentsSettings> settings)
{
    public string Currency { get; } = Money.NormalizeCurrency(settings.Value.Currency);

    public Task<Wallet?> FindUserWalletAsync(string userId, CancellationToken cancellationToken = default)
        => db.Wallets.FirstOrDefaultAsync(w => w.Type == WalletType.User && w.OwnerUserId == userId && w.Currency == Currency, cancellationToken);

    /// <summary>The user's wallet, added to the unit of work if it doesn't exist yet.</summary>
    public async Task<Wallet> GetOrCreateUserWalletAsync(string userId, CancellationToken cancellationToken = default)
    {
        var wallet = await FindUserWalletAsync(userId, cancellationToken)
                     ?? db.Wallets.Local.FirstOrDefault(w => w.Type == WalletType.User && w.OwnerUserId == userId && w.Currency == Currency);
        if (wallet != null)
            return wallet;

        wallet = Wallet.ForUser(userId, Currency, Now);
        db.Wallets.Add(wallet);
        return wallet;
    }

    public async Task<Wallet> GetSystemWalletAsync(WalletType type, CancellationToken cancellationToken = default)
    {
        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Type == type && w.Currency == Currency, cancellationToken)
                     ?? db.Wallets.Local.FirstOrDefault(w => w.Type == type && w.Currency == Currency);
        if (wallet != null)
            return wallet;

        wallet = Wallet.System(type, Currency, Now);
        db.Wallets.Add(wallet);
        return wallet;
    }

    /// <summary>Validates and records a balanced transaction, updating the wallets' cached balances.</summary>
    /// <exception cref="DomainException">Unbalanced, wrong currency, invalid amount, or a user wallet would go negative.</exception>
    public LedgerTransaction Post(LedgerTransactionType type, string description, string? reference, string? actorUserId,
        IReadOnlyCollection<Posting> postings, Guid? reversalOfId = null, bool allowOverdraft = false)
    {
        if (postings.Count < 2)
            throw new DomainException(DomainErrors.Unbalanced, "A transaction needs at least one debit and one credit.");
        if (postings.Any(p => p.Wallet.Currency != Currency))
            throw new DomainException(DomainErrors.CurrencyMismatch, "All entries of a transaction must use the platform currency.");
        if (postings.Any(p => p.Amount <= 0 || !Money.HasValidScale(p.Amount)))
            throw new DomainException(DomainErrors.InvalidAmount, "Amounts must be positive with at most 2 decimal places.");

        var debits = postings.Where(p => p.Direction == EntryDirection.Debit).Sum(p => p.Amount);
        var credits = postings.Where(p => p.Direction == EntryDirection.Credit).Sum(p => p.Amount);
        if (debits != credits)
            throw new DomainException(DomainErrors.Unbalanced, "Debits and credits of a transaction must be equal.");

        var now = Now;
        var transaction = new LedgerTransaction
        {
            Type = type,
            Currency = Currency,
            Description = description,
            Reference = reference,
            ReversalOfId = reversalOfId,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
        };

        // Debits first: an overdraft fails before any credit is applied
        foreach (var posting in postings.OrderBy(p => p.Direction))
        {
            var balanceAfter = posting.Wallet.Apply(posting.Direction, posting.Amount, now, allowOverdraft);
            transaction.Entries.Add(new LedgerEntry
            {
                Wallet = posting.Wallet,
                WalletId = posting.Wallet.Id,
                Direction = posting.Direction,
                Amount = posting.Amount,
                Currency = Currency,
                BalanceAfter = balanceAfter,
                CreatedAt = now,
            });
        }

        db.LedgerTransactions.Add(transaction);
        return transaction;
    }

    /// <summary>
    /// Posts the mirror image of an earlier transaction (every debit becomes a credit and vice versa).
    /// Nothing is deleted: the original and its reversal both stay in the ledger.
    /// </summary>
    /// <exception cref="DomainException">The transaction is already reversed, or a wallet lacks the funds.</exception>
    public async Task<LedgerTransaction> ReverseAsync(Guid transactionId, string description, string? actorUserId,
        bool allowOverdraft = false, CancellationToken cancellationToken = default)
    {
        var original = await db.LedgerTransactions
            .Include(t => t.Entries).ThenInclude(e => e.Wallet)
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken)
            ?? throw new InvalidOperationException($"Ledger transaction {transactionId} does not exist.");

        if (await IsReversedAsync(transactionId, cancellationToken))
            throw new DomainException("Ledger.AlreadyReversed", "The transaction has already been reversed.");

        var postings = original.Entries
            .Select(e => new Posting(e.Wallet, e.Direction == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit, e.Amount))
            .ToList();
        return Post(LedgerTransactionType.Reversal, description, original.Reference, actorUserId, postings, original.Id, allowOverdraft);
    }

    /// <summary>True when a reversal of the transaction exists that has not itself been reversed.</summary>
    public Task<bool> IsReversedAsync(Guid transactionId, CancellationToken cancellationToken = default)
        => db.LedgerTransactions.AnyAsync(r => r.ReversalOfId == transactionId
                                              && !db.LedgerTransactions.Any(c => c.ReversalOfId == r.Id), cancellationToken);

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
}
