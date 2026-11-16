using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Services.Payments;

public interface IPaymentService
{
    PaymentsConfigDto GetConfig();
    /// <summary>Starts a wallet top-up; the wallet is credited when the provider confirms it (webhook).</summary>
    Task<Result<TopUpDto>> CreateTopUpAsync(string userId, CreateTopUpDto dto, CancellationToken cancellationToken = default);
    Task<Result<PaymentDto>> GetMyPaymentAsync(string userId, Guid paymentId, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<PaymentDto>>> GetMyPaymentsAsync(string userId, PaymentQuery query, string baseUrl, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<PaymentDto>>> GetAllAsync(PaymentQuery query, string baseUrl, CancellationToken cancellationToken = default);
    /// <summary>Refunds a succeeded top-up in full: takes the money back from the wallet, then refunds it at the provider.</summary>
    Task<Result<PaymentDto>> RefundAsync(Guid paymentId, string adminUserId, CancellationToken cancellationToken = default);
    /// <summary>Applies a verified provider event. Safe to call again with the same event.</summary>
    Task<Result> HandleWebhookAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default);
}

/// <remarks>Logs carry ids and statuses only: never amounts, client secrets or keys.</remarks>
public sealed class PaymentService(
    IAppDbContext db,
    Ledger ledger,
    FinanceTransaction finance,
    IPaymentGateway gateway,
    IOptions<PaymentsSettings> paymentsSettings,
    IOptions<StripeSettings> stripeSettings,
    TimeProvider timeProvider,
    ILogger<PaymentService> logger) : IPaymentService
{
    private PaymentsSettings Settings => paymentsSettings.Value;
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public PaymentsConfigDto GetConfig() => new(ledger.Currency, stripeSettings.Value.PublishableKey,
        Settings.MinTopUp, Settings.MaxTopUp, Settings.MinPayout, Settings.PlatformFeePercent);

    public async Task<Result<TopUpDto>> CreateTopUpAsync(string userId, CreateTopUpDto dto, CancellationToken cancellationToken = default)
    {
        // 1. Record the intent first, so every provider call can be traced back to a payment row
        var created = await finance.ExecuteAsync(async ct =>
        {
            var wallet = await ledger.GetOrCreateUserWalletAsync(userId, ct);
            var payment = new Payment
            {
                UserId = userId,
                Wallet = wallet,
                WalletId = wallet.Id,
                Amount = dto.Amount,
                Currency = ledger.Currency,
                Status = PaymentStatus.Pending,
                CreatedAt = Now,
                UpdatedAt = Now,
            };
            db.Payments.Add(payment);
            return Result<Payment>.Success(payment);
        }, cancellationToken);
        if (created.IsFailure)
            return Result<TopUpDto>.From(created);
        var payment = created.Data!;

        // 2. Ask the provider for a PaymentIntent; our payment id doubles as its idempotency key
        ProviderPaymentIntent intent;
        try
        {
            intent = await gateway.CreatePaymentIntentAsync(
                new CreatePaymentIntentRequest(payment.Id, userId, payment.Amount, payment.Currency, $"topup-{payment.Id}"), cancellationToken);
        }
        catch (PaymentProviderException ex)
        {
            logger.LogWarning("Payment {PaymentId}: provider rejected the top-up: {Reason}", payment.Id, ex.Message);
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "The payment provider is unavailable.";
            payment.UpdatedAt = Now;
            await db.SaveChangesAsync(CancellationToken.None);
            return Result<TopUpDto>.Fail("The payment provider is unavailable. Please try again later.", ErrorType.Unavailable, ErrorCodes.PaymentProviderError);
        }

        payment.ProviderPaymentId = intent.ProviderPaymentId;
        payment.UpdatedAt = Now;
        await db.SaveChangesAsync(CancellationToken.None);
        logger.LogInformation("Payment {PaymentId} created as {ProviderPaymentId}", payment.Id, intent.ProviderPaymentId);

        return Result<TopUpDto>.Success(new TopUpDto(payment.Id, payment.Amount, payment.Currency, payment.Status,
            intent.ClientSecret, stripeSettings.Value.PublishableKey));
    }

    public async Task<Result<PaymentDto>> GetMyPaymentAsync(string userId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.AsNoTracking().Where(p => p.Id == paymentId && p.UserId == userId)
            .Select(PaymentMappings.PaymentToDto).FirstOrDefaultAsync(cancellationToken);
        return payment == null
            ? Result<PaymentDto>.NotFound("Payment not found", ErrorCodes.PaymentNotFound)
            : Result<PaymentDto>.Success(payment);
    }

    public Task<Result<PaginatedResult<PaymentDto>>> GetMyPaymentsAsync(string userId, PaymentQuery query, string baseUrl, CancellationToken cancellationToken = default)
        => PageAsync(db.Payments.Where(p => p.UserId == userId), query, baseUrl, cancellationToken);

    public Task<Result<PaginatedResult<PaymentDto>>> GetAllAsync(PaymentQuery query, string baseUrl, CancellationToken cancellationToken = default)
    {
        var payments = db.Payments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.UserId))
            payments = payments.Where(p => p.UserId == query.UserId);
        return PageAsync(payments, query, baseUrl, cancellationToken);
    }

    public async Task<Result<PaymentDto>> RefundAsync(Guid paymentId, string adminUserId, CancellationToken cancellationToken = default)
    {
        // 1. Take the money back from the wallet first, so it can't be spent while the provider refunds it
        var reserved = await finance.ExecuteAsync(async ct =>
        {
            var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
            if (payment == null)
                return Result<Payment>.NotFound("Payment not found", ErrorCodes.PaymentNotFound);
            if (payment.Status != PaymentStatus.Succeeded || payment.LedgerTransactionId == null || payment.ProviderPaymentId == null)
                return Result<Payment>.Conflict($"Only succeeded payments can be refunded (status: {payment.Status}).", ErrorCodes.PaymentNotRefundable);

            var reversal = await ledger.ReverseAsync(payment.LedgerTransactionId.Value, $"Refund of top-up {payment.Id}", adminUserId, cancellationToken: ct);
            payment.Status = PaymentStatus.Refunded;
            payment.RefundTransactionId = reversal.Id;
            payment.UpdatedAt = Now;
            return Result<Payment>.Success(payment);
        }, cancellationToken);
        if (reserved.IsFailure)
            return Result<PaymentDto>.From(reserved);
        var refunded = reserved.Data!;

        // 2. Refund at the provider; if that fails, give the money back to the wallet (compensating entry)
        try
        {
            await gateway.RefundAsync(new RefundRequest(refunded.Id, refunded.ProviderPaymentId!, refunded.Amount, refunded.Currency,
                $"refund-{refunded.Id}-{refunded.RefundTransactionId}"), cancellationToken);
            logger.LogInformation("Payment {PaymentId} refunded by {AdminId}", refunded.Id, adminUserId);
        }
        catch (PaymentProviderException ex)
        {
            logger.LogWarning("Payment {PaymentId}: provider refund failed, compensating: {Reason}", refunded.Id, ex.Message);
            var compensated = await finance.ExecuteAsync(async ct =>
            {
                var payment = await db.Payments.FirstAsync(p => p.Id == paymentId, ct);
                await ledger.ReverseAsync(payment.RefundTransactionId!.Value, $"Refund of top-up {payment.Id} failed at the provider", adminUserId, cancellationToken: ct);
                payment.Status = PaymentStatus.Succeeded;
                payment.RefundTransactionId = null;
                payment.UpdatedAt = Now;
                return Result<bool>.Success(true);
            }, CancellationToken.None);
            if (compensated.IsFailure)
                logger.LogError("Payment {PaymentId}: compensation failed ({Code}); manual review needed", refunded.Id, compensated.Error.Code);

            return Result<PaymentDto>.Fail("The payment provider could not refund the payment. Please try again later.", ErrorType.Unavailable, ErrorCodes.PaymentProviderError);
        }

        return await GetDtoAsync(paymentId, cancellationToken);
    }

    public async Task<Result> HandleWebhookAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        var result = await finance.ExecuteAsync(async ct =>
        {
            // Providers deliver at least once: remember handled events (the primary key also stops concurrent duplicates)
            if (await db.ProcessedWebhookEvents.AnyAsync(e => e.Id == webhookEvent.EventId, ct))
                return Result<string>.Success("duplicate");
            db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
            {
                Id = webhookEvent.EventId,
                Provider = Payment.StripeProvider,
                Type = webhookEvent.Type,
                ProcessedAt = Now,
            });

            if (webhookEvent.Kind == PaymentWebhookKind.Ignored || webhookEvent.ProviderPaymentId == null)
                return Result<string>.Success("ignored");

            var payment = await db.Payments.FirstOrDefaultAsync(p => p.ProviderPaymentId == webhookEvent.ProviderPaymentId, ct);
            if (payment == null)
                return Result<string>.Success("unknown payment");

            return Result<string>.Success(webhookEvent.Kind switch
            {
                PaymentWebhookKind.PaymentSucceeded => await ApplySucceededAsync(payment, webhookEvent, ct),
                PaymentWebhookKind.PaymentFailed => ApplyFailed(payment, webhookEvent),
                PaymentWebhookKind.PaymentRefunded => await ApplyRefundedAsync(payment, webhookEvent, ct),
                _ => "ignored",
            });
        }, cancellationToken);

        if (result.IsSuccess)
            logger.LogInformation("Webhook {EventId} ({Type}): {Outcome}", webhookEvent.EventId, webhookEvent.Type, result.Data);
        return result;
    }

    private async Task<string> ApplySucceededAsync(Payment payment, PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        // A failed attempt can be followed by a successful one on the same PaymentIntent
        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Failed))
            return $"already {payment.Status}";

        if (webhookEvent.Amount != payment.Amount || !string.Equals(webhookEvent.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("Payment {PaymentId}: provider amount or currency differs from the payment; not credited", payment.Id);
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "Amount mismatch; held for manual review.";
            payment.UpdatedAt = Now;
            return "amount mismatch";
        }

        var userWallet = await db.Wallets.FirstAsync(w => w.Id == payment.WalletId, ct);
        var clearing = await ledger.GetSystemWalletAsync(WalletType.ExternalClearing, ct);
        var transaction = ledger.Post(LedgerTransactionType.TopUp, "Wallet top-up", $"Payment:{payment.Id}", payment.UserId,
        [
            new Posting(clearing, EntryDirection.Debit, payment.Amount),
            new Posting(userWallet, EntryDirection.Credit, payment.Amount),
        ]);

        payment.Status = PaymentStatus.Succeeded;
        payment.FailureReason = null;
        payment.LedgerTransactionId = transaction.Id;
        payment.UpdatedAt = Now;
        return "credited";
    }

    private string ApplyFailed(Payment payment, PaymentWebhookEvent webhookEvent)
    {
        if (payment.Status != PaymentStatus.Pending)
            return $"already {payment.Status}";

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = Truncate(webhookEvent.FailureReason ?? "The payment failed.", 500);
        payment.UpdatedAt = Now;
        return "failed";
    }

    /// <summary>A refund made outside the platform (e.g. in the Stripe dashboard). Refunds made through the API are already applied.</summary>
    private async Task<string> ApplyRefundedAsync(Payment payment, PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        if (payment.Status != PaymentStatus.Succeeded || payment.LedgerTransactionId == null)
            return $"already {payment.Status}";

        if (webhookEvent.Amount != payment.Amount)
        {
            logger.LogWarning("Payment {PaymentId}: partial refund at the provider; needs manual review", payment.Id);
            return "partial refund ignored";
        }

        // The money has already left: record it even if the user spent it (the wallet goes negative = debt)
        var reversal = await ledger.ReverseAsync(payment.LedgerTransactionId.Value, $"Refund of top-up {payment.Id} (provider)", null,
            allowOverdraft: true, cancellationToken: ct);
        payment.Status = PaymentStatus.Refunded;
        payment.RefundTransactionId = reversal.Id;
        payment.UpdatedAt = Now;
        return "refunded";
    }

    private async Task<Result<PaymentDto>> GetDtoAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var dto = await db.Payments.AsNoTracking().Where(p => p.Id == paymentId)
            .Select(PaymentMappings.PaymentToDto).FirstAsync(cancellationToken);
        return Result<PaymentDto>.Success(dto);
    }

    private static async Task<Result<PaginatedResult<PaymentDto>>> PageAsync(IQueryable<Payment> payments, PaymentQuery query, string baseUrl, CancellationToken cancellationToken)
    {
        if (query.Status is { } status)
            payments = payments.Where(p => p.Status == status);

        var page = await payments.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(PaymentMappings.PaymentToDto)
            .ToPaginatedResultAsync(query.PageNumber, query.PageSize, baseUrl, cancellationToken);
        return Result<PaginatedResult<PaymentDto>>.Success(page);
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
