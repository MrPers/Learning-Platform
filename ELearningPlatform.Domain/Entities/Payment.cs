namespace ELearningPlatform.Domain.Entities;

/// <summary>A wallet top-up through the external payment provider (a Stripe PaymentIntent).</summary>
public class Payment : IAuditable
{
    public const string StripeProvider = "Stripe";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public Guid WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Provider { get; set; } = StripeProvider;
    /// <summary>Provider's id (Stripe PaymentIntent "pi_...").</summary>
    public string? ProviderPaymentId { get; set; }
    public string? FailureReason { get; set; }
    /// <summary>Ledger transaction that credited the wallet, once succeeded.</summary>
    public Guid? LedgerTransactionId { get; set; }
    public Guid? RefundTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
