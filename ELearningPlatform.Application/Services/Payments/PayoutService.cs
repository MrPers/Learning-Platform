using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELearningPlatform.Application.Services.Payments;

public interface IPayoutService
{
    /// <summary>Reserves the amount from the teacher's wallet (moved to the payout hold) until an admin decides.</summary>
    Task<Result<PayoutDto>> RequestAsync(string teacherUserId, RequestPayoutDto dto, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<PayoutDto>>> GetMyPayoutsAsync(string teacherUserId, PayoutQuery query, string baseUrl, CancellationToken cancellationToken = default);
    Task<Result<TeacherEarningsDto>> GetEarningsAsync(string teacherUserId, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<PayoutDto>>> GetAllAsync(PayoutQuery query, string baseUrl, CancellationToken cancellationToken = default);
    /// <summary>Marks the payout as paid: the held money leaves the platform.</summary>
    Task<Result<PayoutDto>> ApproveAsync(Guid payoutId, string adminUserId, CancellationToken cancellationToken = default);
    /// <summary>Returns the held money to the teacher's wallet (reversal of the request).</summary>
    Task<Result<PayoutDto>> RejectAsync(Guid payoutId, string adminUserId, RejectPayoutDto dto, CancellationToken cancellationToken = default);
}

public sealed class PayoutService(
    IAppDbContext db,
    Ledger ledger,
    FinanceTransaction finance,
    TimeProvider timeProvider,
    ILogger<PayoutService> logger) : IPayoutService
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<PayoutDto>> RequestAsync(string teacherUserId, RequestPayoutDto dto, CancellationToken cancellationToken = default)
    {
        var result = await finance.ExecuteAsync(async ct =>
        {
            if (!await db.Teachers.AnyAsync(t => t.ApplicationUserId == teacherUserId, ct))
                return Result<Payout>.Forbidden("Only teachers can request payouts", ErrorCodes.NotATeacher);

            var wallet = await ledger.FindUserWalletAsync(teacherUserId, ct);
            if (wallet == null || wallet.Balance < dto.Amount)
                return Result<Payout>.Conflict("Insufficient funds", ErrorCodes.InsufficientFunds);

            var payout = new Payout
            {
                TeacherUserId = teacherUserId,
                WalletId = wallet.Id,
                Amount = dto.Amount,
                Currency = ledger.Currency,
                Status = PayoutStatus.Pending,
                CreatedAt = Now,
            };
            var hold = await ledger.GetSystemWalletAsync(WalletType.PayoutHold, ct);
            var transaction = ledger.Post(LedgerTransactionType.PayoutRequest, "Payout request", $"Payout:{payout.Id}", teacherUserId,
            [
                new Posting(wallet, EntryDirection.Debit, dto.Amount),
                new Posting(hold, EntryDirection.Credit, dto.Amount),
            ]);
            payout.RequestTransactionId = transaction.Id;
            db.Payouts.Add(payout);
            return Result<Payout>.Success(payout);
        }, cancellationToken);

        if (result.IsFailure)
            return Result<PayoutDto>.From(result);

        logger.LogInformation("Payout {PayoutId} requested", result.Data!.Id);
        return await GetDtoAsync(result.Data.Id, cancellationToken);
    }

    public Task<Result<PaginatedResult<PayoutDto>>> GetMyPayoutsAsync(string teacherUserId, PayoutQuery query, string baseUrl, CancellationToken cancellationToken = default)
        => PageAsync(db.Payouts.Where(p => p.TeacherUserId == teacherUserId), query, baseUrl, cancellationToken);

    public Task<Result<PaginatedResult<PayoutDto>>> GetAllAsync(PayoutQuery query, string baseUrl, CancellationToken cancellationToken = default)
        => PageAsync(db.Payouts, query, baseUrl, cancellationToken);

    public async Task<Result<TeacherEarningsDto>> GetEarningsAsync(string teacherUserId, CancellationToken cancellationToken = default)
    {
        if (!await db.Teachers.AnyAsync(t => t.ApplicationUserId == teacherUserId, cancellationToken))
            return Result<TeacherEarningsDto>.Forbidden("Only teachers have earnings", ErrorCodes.NotATeacher);

        var wallet = await ledger.FindUserWalletAsync(teacherUserId, cancellationToken);

        // Sums in memory: SQLite has no native decimal type
        var sales = await db.CoursePurchases.AsNoTracking()
            .Where(p => p.Course.Teacher.ApplicationUserId == teacherUserId && p.Status == PurchaseStatus.Completed && p.LedgerTransactionId != null)
            .Select(p => new { p.CourseId, CourseName = p.Course.Name, p.TeacherAmount })
            .ToListAsync(cancellationToken);
        var payouts = await db.Payouts.AsNoTracking()
            .Where(p => p.TeacherUserId == teacherUserId && p.Status != PayoutStatus.Rejected)
            .Select(p => new { p.Status, p.Amount })
            .ToListAsync(cancellationToken);

        var courses = sales.GroupBy(s => new { s.CourseId, s.CourseName })
            .Select(g => new CourseEarningsDto(g.Key.CourseId, g.Key.CourseName, g.Count(), g.Sum(s => s.TeacherAmount)))
            .OrderByDescending(c => c.Earned)
            .ToList();

        return Result<TeacherEarningsDto>.Success(new TeacherEarningsDto(
            ledger.Currency,
            wallet?.Balance ?? 0m,
            sales.Sum(s => s.TeacherAmount),
            payouts.Where(p => p.Status == PayoutStatus.Pending).Sum(p => p.Amount),
            payouts.Where(p => p.Status == PayoutStatus.Completed).Sum(p => p.Amount),
            sales.Count,
            courses));
    }

    public async Task<Result<PayoutDto>> ApproveAsync(Guid payoutId, string adminUserId, CancellationToken cancellationToken = default)
    {
        var result = await finance.ExecuteAsync(async ct =>
        {
            var payout = await db.Payouts.FirstOrDefaultAsync(p => p.Id == payoutId, ct);
            if (payout == null)
                return Result<Payout>.NotFound("Payout not found", ErrorCodes.PayoutNotFound);
            if (payout.Status != PayoutStatus.Pending)
                return Result<Payout>.Conflict($"The payout is already {payout.Status}", ErrorCodes.PayoutNotPending);

            var hold = await ledger.GetSystemWalletAsync(WalletType.PayoutHold, ct);
            var clearing = await ledger.GetSystemWalletAsync(WalletType.ExternalClearing, ct);
            var transaction = ledger.Post(LedgerTransactionType.PayoutCompletion, "Payout sent", $"Payout:{payout.Id}", adminUserId,
            [
                new Posting(hold, EntryDirection.Debit, payout.Amount),
                new Posting(clearing, EntryDirection.Credit, payout.Amount),
            ]);

            payout.Status = PayoutStatus.Completed;
            payout.SettlementTransactionId = transaction.Id;
            payout.ProcessedByUserId = adminUserId;
            payout.ProcessedAt = Now;
            return Result<Payout>.Success(payout);
        }, cancellationToken);

        if (result.IsFailure)
            return Result<PayoutDto>.From(result);

        logger.LogInformation("Payout {PayoutId} approved by {AdminId}", payoutId, adminUserId);
        return await GetDtoAsync(payoutId, cancellationToken);
    }

    public async Task<Result<PayoutDto>> RejectAsync(Guid payoutId, string adminUserId, RejectPayoutDto dto, CancellationToken cancellationToken = default)
    {
        var result = await finance.ExecuteAsync(async ct =>
        {
            var payout = await db.Payouts.FirstOrDefaultAsync(p => p.Id == payoutId, ct);
            if (payout == null)
                return Result<Payout>.NotFound("Payout not found", ErrorCodes.PayoutNotFound);
            if (payout.Status != PayoutStatus.Pending)
                return Result<Payout>.Conflict($"The payout is already {payout.Status}", ErrorCodes.PayoutNotPending);

            var reversal = await ledger.ReverseAsync(payout.RequestTransactionId, "Payout rejected", adminUserId, cancellationToken: ct);
            payout.Status = PayoutStatus.Rejected;
            payout.RejectionReason = dto.Reason.Trim();
            payout.SettlementTransactionId = reversal.Id;
            payout.ProcessedByUserId = adminUserId;
            payout.ProcessedAt = Now;
            return Result<Payout>.Success(payout);
        }, cancellationToken);

        if (result.IsFailure)
            return Result<PayoutDto>.From(result);

        logger.LogInformation("Payout {PayoutId} rejected by {AdminId}", payoutId, adminUserId);
        return await GetDtoAsync(payoutId, cancellationToken);
    }

    private async Task<Result<PayoutDto>> GetDtoAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        var dto = await db.Payouts.AsNoTracking().Where(p => p.Id == payoutId)
            .Select(PaymentMappings.PayoutToDto).FirstAsync(cancellationToken);
        return Result<PayoutDto>.Success(dto);
    }

    private static async Task<Result<PaginatedResult<PayoutDto>>> PageAsync(IQueryable<Payout> payouts, PayoutQuery query, string baseUrl, CancellationToken cancellationToken)
    {
        if (query.Status is { } status)
            payouts = payouts.Where(p => p.Status == status);

        var page = await payouts.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(PaymentMappings.PayoutToDto)
            .ToPaginatedResultAsync(query.PageNumber, query.PageSize, baseUrl, cancellationToken);
        return Result<PaginatedResult<PayoutDto>>.Success(page);
    }
}
