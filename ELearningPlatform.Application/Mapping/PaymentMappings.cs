using System.Linq.Expressions;

namespace ELearningPlatform.Application.Mapping;

/// <summary>EF-translatable projections for the payment domain (see <see cref="Mappings"/>).</summary>
public static class PaymentMappings
{
    public static readonly Expression<Func<LedgerEntry, StatementLineDto>> EntryToStatementLine = e => new StatementLineDto(
        e.Id,
        e.TransactionId,
        e.Transaction.Type,
        e.Direction,
        e.Amount,
        e.Currency,
        e.BalanceAfter,
        e.Transaction.Description,
        e.Transaction.Reference,
        e.CreatedAt);

    public static readonly Expression<Func<Payment, PaymentDto>> PaymentToDto = p => new PaymentDto(
        p.Id,
        p.UserId,
        p.User.Email,
        p.Amount,
        p.Currency,
        p.Status,
        p.Provider,
        p.ProviderPaymentId,
        p.FailureReason,
        p.CreatedAt,
        p.UpdatedAt);

    public static readonly Expression<Func<CoursePurchase, PurchaseDto>> PurchaseToDto = p => new PurchaseDto(
        p.Id,
        p.CourseId,
        p.Course.Name,
        p.BuyerUserId,
        p.Price,
        p.PlatformFee,
        p.TeacherAmount,
        p.Currency,
        p.Status,
        p.CreatedAt,
        p.RefundedAt);

    public static readonly Expression<Func<Payout, PayoutDto>> PayoutToDto = p => new PayoutDto(
        p.Id,
        p.TeacherUserId,
        p.TeacherUser.FirstName + " " + p.TeacherUser.LastName,
        p.Amount,
        p.Currency,
        p.Status,
        p.RejectionReason,
        p.CreatedAt,
        p.ProcessedAt);

    public static readonly Expression<Func<AuditLog, AuditLogDto>> AuditLogToDto = a => new AuditLogDto(
        a.Id,
        a.UserId,
        a.Action,
        a.EntityType,
        a.EntityId,
        a.OldValues,
        a.NewValues,
        a.IpAddress,
        a.Timestamp);
}
