namespace ELearningPlatform.Domain.Entities;

/// <summary>
/// An account in the ledger. <see cref="Balance"/> is a cache of (credits - debits) over the wallet's
/// ledger entries; it only changes through <see cref="Apply"/>, which also bumps <see cref="RowVersion"/>
/// so concurrent writers based on a stale balance fail with a concurrency conflict instead of double-spending.
/// </summary>
public class Wallet : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public WalletType Type { get; set; }
    /// <summary>Owner for <see cref="WalletType.User"/> wallets; null for system wallets.</summary>
    public string? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>Optimistic concurrency token, incremented on every balance change.</summary>
    public long RowVersion { get; private set; }

    public ICollection<LedgerEntry> Entries { get; set; } = [];

    public bool CanGoNegative => Type != WalletType.User;

    public static Wallet ForUser(string userId, string currency, DateTime now) => new()
    {
        Type = WalletType.User,
        OwnerUserId = userId,
        Currency = Money.NormalizeCurrency(currency),
        CreatedAt = now,
        UpdatedAt = now,
    };

    public static Wallet System(WalletType type, string currency, DateTime now) => type == WalletType.User
        ? throw new ArgumentException("User wallets need an owner.", nameof(type))
        : new() { Type = type, Currency = Money.NormalizeCurrency(currency), CreatedAt = now, UpdatedAt = now };

    /// <summary>Applies one ledger entry and returns the balance after it. Only the ledger poster calls this.</summary>
    /// <param name="allowOverdraft">
    /// Lets a user wallet go negative. Only for corrections forced from outside (a refund issued in the
    /// provider's dashboard after the money was spent): the debt is recorded instead of being lost.
    /// </param>
    public decimal Apply(EntryDirection direction, decimal amount, DateTime now, bool allowOverdraft = false)
    {
        if (amount <= 0 || !Money.HasValidScale(amount))
            throw new DomainException(DomainErrors.InvalidAmount, "Amounts must be positive with at most 2 decimal places.");

        var newBalance = direction == EntryDirection.Credit ? Balance + amount : Balance - amount;
        if (newBalance < 0 && !CanGoNegative && !allowOverdraft)
            throw new DomainException(DomainErrors.InsufficientFunds, "Insufficient funds.");

        Balance = newBalance;
        UpdatedAt = now;
        RowVersion++;
        return newBalance;
    }
}
