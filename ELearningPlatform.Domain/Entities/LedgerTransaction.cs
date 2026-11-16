namespace ELearningPlatform.Domain.Entities;

/// <summary>
/// One balanced movement of money: the sum of its debit entries equals the sum of its credit entries.
/// Append-only: never updated or deleted; refunds and corrections are new reversal transactions.
/// </summary>
public class LedgerTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LedgerTransactionType Type { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>The business object that caused it, e.g. "Payment:{id}".</summary>
    public string? Reference { get; set; }
    /// <summary>For <see cref="LedgerTransactionType.Reversal"/>: the transaction being undone.</summary>
    public Guid? ReversalOfId { get; set; }
    public LedgerTransaction? ReversalOf { get; set; }
    /// <summary>User whose request produced it (null for webhooks and system jobs).</summary>
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<LedgerEntry> Entries { get; set; } = [];
}
