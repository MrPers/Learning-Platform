namespace ELearningPlatform.Application.DTOs.Payments;

public sealed record WalletDto(Guid Id, decimal Balance, string Currency, DateTime UpdatedAt);

public class CreateTopUpDto
{
    public decimal Amount { get; set; }
}

/// <param name="ClientSecret">Pass to Stripe.js (confirmPayment) to let the payer complete the payment.</param>
public sealed record TopUpDto(Guid PaymentId, decimal Amount, string Currency, PaymentStatus Status, string ClientSecret, string PublishableKey);

public sealed record PaymentDto(
    Guid Id,
    string UserId,
    string? UserEmail,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string Provider,
    string? ProviderPaymentId,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record PaymentsConfigDto(string Currency, string PublishableKey, decimal MinTopUp, decimal MaxTopUp, decimal MinPayout, decimal PlatformFeePercent);

public class PaymentQuery
{
    public PaymentStatus? Status { get; set; }
    public string? UserId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = PaginationExtensions.DefaultPageSize;
}

public sealed record PurchaseDto(
    Guid Id,
    int CourseId,
    string CourseName,
    string BuyerUserId,
    decimal Price,
    decimal PlatformFee,
    decimal TeacherAmount,
    string Currency,
    PurchaseStatus Status,
    DateTime CreatedAt,
    DateTime? RefundedAt);

public class PurchaseQuery
{
    public PurchaseStatus? Status { get; set; }
    public int? CourseId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = PaginationExtensions.DefaultPageSize;
}

public class RequestPayoutDto
{
    public decimal Amount { get; set; }
}

public class RejectPayoutDto
{
    public string Reason { get; set; } = string.Empty;
}

public sealed record PayoutDto(
    Guid Id,
    string TeacherUserId,
    string TeacherName,
    decimal Amount,
    string Currency,
    PayoutStatus Status,
    string? RejectionReason,
    DateTime CreatedAt,
    DateTime? ProcessedAt);

public class PayoutQuery
{
    public PayoutStatus? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = PaginationExtensions.DefaultPageSize;
}

/// <param name="TotalEarned">Teacher's share of completed (not refunded) sales.</param>
public sealed record TeacherEarningsDto(
    string Currency,
    decimal Balance,
    decimal TotalEarned,
    decimal PendingPayouts,
    decimal PaidOut,
    int SalesCount,
    IReadOnlyList<CourseEarningsDto> Courses);

public sealed record CourseEarningsDto(int CourseId, string CourseName, int SalesCount, decimal Earned);

/// <summary>Filters for the wallet statement and its CSV export.</summary>
public class StatementQuery
{
    /// <summary>Inclusive lower bound (UTC).</summary>
    public DateTime? From { get; set; }
    /// <summary>Exclusive upper bound (UTC).</summary>
    public DateTime? To { get; set; }
    public LedgerTransactionType? Type { get; set; }
    public EntryDirection? Direction { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = PaginationExtensions.DefaultPageSize;
}

public sealed record StatementLineDto(
    long EntryId,
    Guid TransactionId,
    LedgerTransactionType Type,
    EntryDirection Direction,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description,
    string? Reference,
    DateTime CreatedAt);

public sealed record CsvFileDto(byte[] Content, string FileName)
{
    public const string ContentType = "text/csv";
}

public sealed record ReconciliationDto(bool IsConsistent, decimal LedgerTotal, int WalletCount, int TransactionCount,
    IReadOnlyList<WalletMismatchDto> Mismatches, IReadOnlyList<Guid> UnbalancedTransactions);

public sealed record WalletMismatchDto(Guid WalletId, WalletType Type, string? OwnerUserId, decimal CachedBalance, decimal LedgerBalance);

public sealed record AuditLogDto(
    long Id,
    string? UserId,
    string Action,
    string EntityType,
    string EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTime Timestamp);

public class AuditLogQuery
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = PaginationExtensions.DefaultPageSize;
}
