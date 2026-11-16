namespace ELearningPlatform.Domain.Entities;

/// <summary>A student's purchase of a course; the price is split into the teacher's share and the platform fee.</summary>
public class CoursePurchase : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BuyerUserId { get; set; } = string.Empty;
    public ApplicationUser BuyerUser { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public decimal Price { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal TeacherAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Completed;
    /// <summary>Null for free courses (nothing to post).</summary>
    public Guid? LedgerTransactionId { get; set; }
    public Guid? RefundTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
}
