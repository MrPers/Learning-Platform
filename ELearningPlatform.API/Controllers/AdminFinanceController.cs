namespace ELearningPlatform.API.Controllers;

/// <summary>Back office: payments, refunds, payouts, audit log and ledger reconciliation.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = PoliciesConstants.AdminOnly)]
public class AdminFinanceController(
    IPaymentService paymentService,
    IPurchaseService purchaseService,
    IPayoutService payoutService,
    IAuditLogService auditLogService,
    IWalletService walletService) : ControllerBase
{
    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] PaymentQuery query, CancellationToken cancellationToken)
        => (await paymentService.GetAllAsync(query, this.CurrentUrlWithFilters(), cancellationToken)).ToActionResult(this);

    /// <summary>Refunds a wallet top-up in full (the money must still be in the wallet).</summary>
    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [Idempotent]
    [HttpPost("payments/{id:guid}/refund")]
    public async Task<IActionResult> RefundPayment(Guid id, CancellationToken cancellationToken)
        => (await paymentService.RefundAsync(id, User.GetUserId(), cancellationToken)).ToActionResult(this);

    [HttpGet("purchases")]
    public async Task<IActionResult> GetPurchases([FromQuery] PurchaseQuery query, CancellationToken cancellationToken)
        => (await purchaseService.GetAllAsync(query, this.CurrentUrlWithFilters(), cancellationToken)).ToActionResult(this);

    /// <summary>Refunds a course purchase to the student's wallet and removes the enrollment.</summary>
    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [Idempotent]
    [HttpPost("purchases/{id:guid}/refund")]
    public async Task<IActionResult> RefundPurchase(Guid id, CancellationToken cancellationToken)
        => (await purchaseService.RefundAsync(id, User.GetUserId(), cancellationToken)).ToActionResult(this);

    [HttpGet("payouts")]
    public async Task<IActionResult> GetPayouts([FromQuery] PayoutQuery query, CancellationToken cancellationToken)
        => (await payoutService.GetAllAsync(query, this.CurrentUrlWithFilters(), cancellationToken)).ToActionResult(this);

    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [Idempotent]
    [HttpPost("payouts/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePayout(Guid id, CancellationToken cancellationToken)
        => (await payoutService.ApproveAsync(id, User.GetUserId(), cancellationToken)).ToActionResult(this);

    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [Idempotent]
    [HttpPost("payouts/{id:guid}/reject")]
    public async Task<IActionResult> RejectPayout(Guid id, RejectPayoutDto dto, CancellationToken cancellationToken)
        => (await payoutService.RejectAsync(id, User.GetUserId(), dto, cancellationToken)).ToActionResult(this);

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
        => (await auditLogService.GetAsync(query, this.CurrentUrlWithFilters(), cancellationToken)).ToActionResult(this);

    /// <summary>Ledger health check: cached balances vs entries, balanced transactions, zero total.</summary>
    [HttpGet("finance/reconciliation")]
    public async Task<IActionResult> Reconcile(CancellationToken cancellationToken)
        => (await walletService.ReconcileAsync(cancellationToken)).ToActionResult(this);
}
