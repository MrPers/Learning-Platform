namespace ELearningPlatform.Domain.Common;

/// <summary>A broken money invariant: unbalanced posting, overdraft, wrong currency or scale.</summary>
public sealed class DomainException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public static class DomainErrors
{
    public const string InvalidAmount = "Ledger.InvalidAmount";
    public const string Unbalanced = "Ledger.Unbalanced";
    public const string CurrencyMismatch = "Ledger.CurrencyMismatch";
    public const string InsufficientFunds = "Wallet.InsufficientFunds";
}
