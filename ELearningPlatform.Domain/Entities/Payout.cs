namespace ELearningPlatform.Domain.Entities;

/// <summary>A teacher's withdrawal of earnings. The amount moves to the payout hold when requested.</summary>
public class Payout : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TeacherUserId { get; set; } = string.Empty;
    public ApplicationUser TeacherUser { get; set; } = null!;
    public Guid WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
    public Guid RequestTransactionId { get; set; }
    /// <summary>Completion transaction (approved) or reversal (rejected).</summary>
    public Guid? SettlementTransactionId { get; set; }
    public string? RejectionReason { get; set; }
    public string? ProcessedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
