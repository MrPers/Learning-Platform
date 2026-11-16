using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Services.Payments;

public interface IPurchaseService
{
    /// <summary>Buys a course with the student's wallet and enrolls them, in one database transaction.</summary>
    Task<Result<PurchaseDto>> PurchaseAsync(string userId, int courseId, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<PurchaseDto>>> GetMyPurchasesAsync(string userId, PurchaseQuery query, string baseUrl, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<PurchaseDto>>> GetAllAsync(PurchaseQuery query, string baseUrl, CancellationToken cancellationToken = default);
    /// <summary>Reverses the sale (student refunded, teacher and platform debited) and removes the enrollment.</summary>
    Task<Result<PurchaseDto>> RefundAsync(Guid purchaseId, string adminUserId, CancellationToken cancellationToken = default);
}

public sealed class PurchaseService(
    IAppDbContext db,
    Ledger ledger,
    FinanceTransaction finance,
    IOptions<PaymentsSettings> settings,
    TimeProvider timeProvider,
    ILogger<PurchaseService> logger) : IPurchaseService
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>The platform's commission on a price, rounded to cents; the teacher gets the rest.</summary>
    public static decimal PlatformFee(decimal price, decimal feePercent) => Money.Round(price * feePercent / 100m);

    public async Task<Result<PurchaseDto>> PurchaseAsync(string userId, int courseId, CancellationToken cancellationToken = default)
    {
        var result = await finance.ExecuteAsync(async ct =>
        {
            var student = await db.Students.FirstOrDefaultAsync(s => s.ApplicationUserId == userId, ct);
            if (student == null)
                return Result<CoursePurchase>.Forbidden("Only students can buy courses", ErrorCodes.NotAStudent);

            var course = await db.Courses.Include(c => c.Teacher).FirstOrDefaultAsync(c => c.Id == courseId, ct);
            if (course == null)
                return Result<CoursePurchase>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

            if (await db.StudentCourses.AnyAsync(sc => sc.StudentId == student.Id && sc.CourseId == courseId, ct))
                return Result<CoursePurchase>.Conflict("You already own this course", ErrorCodes.CourseAlreadyPurchased);

            var price = course.Price;
            var purchase = new CoursePurchase
            {
                BuyerUserId = userId,
                CourseId = course.Id,
                Price = price,
                Currency = ledger.Currency,
                Status = PurchaseStatus.Completed,
                CreatedAt = Now,
            };

            if (price > 0)
            {
                var buyerWallet = await ledger.FindUserWalletAsync(userId, ct);
                if (buyerWallet == null || buyerWallet.Balance < price)
                    return Result<CoursePurchase>.Conflict("Insufficient funds", ErrorCodes.InsufficientFunds);

                var fee = PlatformFee(price, settings.Value.PlatformFeePercent);
                purchase.PlatformFee = fee;
                purchase.TeacherAmount = price - fee;

                List<Posting> postings = [new(buyerWallet, EntryDirection.Debit, price)];
                if (purchase.TeacherAmount > 0)
                    postings.Add(new(await ledger.GetOrCreateUserWalletAsync(course.Teacher.ApplicationUserId, ct), EntryDirection.Credit, purchase.TeacherAmount));
                if (fee > 0)
                    postings.Add(new(await ledger.GetSystemWalletAsync(WalletType.PlatformRevenue, ct), EntryDirection.Credit, fee));

                var transaction = ledger.Post(LedgerTransactionType.CoursePurchase, $"Course purchase: {course.Name}",
                    $"Purchase:{purchase.Id}", userId, postings);
                purchase.LedgerTransactionId = transaction.Id;
            }

            db.CoursePurchases.Add(purchase);
            db.StudentCourses.Add(new StudentCourse { StudentId = student.Id, CourseId = course.Id, EnrolledDate = Now });
            return Result<CoursePurchase>.Success(purchase);
        }, cancellationToken);

        if (result.IsFailure)
            return Result<PurchaseDto>.From(result);

        logger.LogInformation("Purchase {PurchaseId} of course {CourseId} completed", result.Data!.Id, courseId);
        return await GetDtoAsync(result.Data.Id, cancellationToken);
    }

    public Task<Result<PaginatedResult<PurchaseDto>>> GetMyPurchasesAsync(string userId, PurchaseQuery query, string baseUrl, CancellationToken cancellationToken = default)
        => PageAsync(db.CoursePurchases.Where(p => p.BuyerUserId == userId), query, baseUrl, cancellationToken);

    public Task<Result<PaginatedResult<PurchaseDto>>> GetAllAsync(PurchaseQuery query, string baseUrl, CancellationToken cancellationToken = default)
        => PageAsync(db.CoursePurchases, query, baseUrl, cancellationToken);

    public async Task<Result<PurchaseDto>> RefundAsync(Guid purchaseId, string adminUserId, CancellationToken cancellationToken = default)
    {
        var result = await finance.ExecuteAsync(async ct =>
        {
            var purchase = await db.CoursePurchases.FirstOrDefaultAsync(p => p.Id == purchaseId, ct);
            if (purchase == null)
                return Result<CoursePurchase>.NotFound("Purchase not found", ErrorCodes.PurchaseNotFound);
            if (purchase.Status != PurchaseStatus.Completed)
                return Result<CoursePurchase>.Conflict("The purchase has already been refunded", ErrorCodes.PurchaseNotRefundable);

            // Fails with Wallet.InsufficientFunds if the teacher has already withdrawn the money
            if (purchase.LedgerTransactionId is { } transactionId)
            {
                var reversal = await ledger.ReverseAsync(transactionId, $"Refund of purchase {purchase.Id}", adminUserId, cancellationToken: ct);
                purchase.RefundTransactionId = reversal.Id;
            }

            purchase.Status = PurchaseStatus.Refunded;
            purchase.RefundedAt = Now;

            // Through the change tracker (not ExecuteDelete), so it commits or rolls back with the rest
            var enrollment = await db.StudentCourses.FirstOrDefaultAsync(sc => sc.Student.ApplicationUserId == purchase.BuyerUserId && sc.CourseId == purchase.CourseId, ct);
            if (enrollment != null)
                db.StudentCourses.Remove(enrollment);

            return Result<CoursePurchase>.Success(purchase);
        }, cancellationToken);

        if (result.IsFailure)
            return Result<PurchaseDto>.From(result);

        logger.LogInformation("Purchase {PurchaseId} refunded by {AdminId}", purchaseId, adminUserId);
        return await GetDtoAsync(purchaseId, cancellationToken);
    }

    private async Task<Result<PurchaseDto>> GetDtoAsync(Guid purchaseId, CancellationToken cancellationToken)
    {
        var dto = await db.CoursePurchases.AsNoTracking().Where(p => p.Id == purchaseId)
            .Select(PaymentMappings.PurchaseToDto).FirstAsync(cancellationToken);
        return Result<PurchaseDto>.Success(dto);
    }

    private static async Task<Result<PaginatedResult<PurchaseDto>>> PageAsync(IQueryable<CoursePurchase> purchases, PurchaseQuery query, string baseUrl, CancellationToken cancellationToken)
    {
        if (query.Status is { } status)
            purchases = purchases.Where(p => p.Status == status);
        if (query.CourseId is { } courseId)
            purchases = purchases.Where(p => p.CourseId == courseId);

        var page = await purchases.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(PaymentMappings.PurchaseToDto)
            .ToPaginatedResultAsync(query.PageNumber, query.PageSize, baseUrl, cancellationToken);
        return Result<PaginatedResult<PurchaseDto>>.Success(page);
    }
}
