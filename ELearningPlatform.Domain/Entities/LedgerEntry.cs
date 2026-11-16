namespace ELearningPlatform.Domain.Entities;

/// <summary>One line of a <see cref="LedgerTransaction"/>: a debit or credit of one wallet. Append-only.</summary>
public class LedgerEntry
{
    /// <summary>Monotonic id; also the order in which entries were applied to a wallet.</summary>
    public long Id { get; set; }
    public Guid TransactionId { get; set; }
    public LedgerTransaction Transaction { get; set; } = null!;
    public Guid WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public EntryDirection Direction { get; set; }
    /// <summary>Always positive; <see cref="Direction"/> gives the sign.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    /// <summary>Wallet balance right after this entry (running balance for statements).</summary>
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; }
}
