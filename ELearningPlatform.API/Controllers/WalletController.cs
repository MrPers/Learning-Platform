namespace ELearningPlatform.API.Controllers;

/// <summary>The caller's wallet: balance, top-ups through Stripe, statement and CSV export.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PoliciesConstants.AdminTeacherStudent)]
public class WalletController(IWalletService walletService, IPaymentService paymentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => (await walletService.GetMyWalletAsync(User.GetUserId(), cancellationToken)).ToActionResult(this);

    /// <summary>Paginated ledger entries of the wallet, newest first.</summary>
    [HttpGet("statement")]
    public async Task<IActionResult> Statement([FromQuery] StatementQuery query, CancellationToken cancellationToken)
        => (await walletService.GetStatementAsync(User.GetUserId(), query, this.CurrentUrlWithFilters(), cancellationToken)).ToActionResult(this);

    [HttpGet("statement/export")]
    public async Task<IActionResult> ExportStatement([FromQuery] StatementQuery query, CancellationToken cancellationToken)
    {
        var result = await walletService.ExportStatementCsvAsync(User.GetUserId(), query, cancellationToken);
        return result.IsSuccess ? File(result.Data!.Content, CsvFileDto.ContentType, result.Data.FileName) : result.ToProblem(this);
    }

    /// <summary>
    /// Starts a top-up. Returns the Stripe client secret to confirm the payment with Stripe.js; the wallet is
    /// credited when Stripe reports success through the webhook. Requires an Idempotency-Key header.
    /// </summary>
    [Authorize(Policy = PoliciesConstants.StudentOnly)]
    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [Idempotent]
    [HttpPost("top-ups")]
    public async Task<IActionResult> TopUp(CreateTopUpDto dto, CancellationToken cancellationToken)
    {
        var result = await paymentService.CreateTopUpAsync(User.GetUserId(), dto, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTopUp), new { id = result.Data!.PaymentId }, result.Data)
            : result.ToProblem(this);
    }

    [HttpGet("top-ups")]
    public async Task<IActionResult> GetTopUps([FromQuery] PaymentQuery query, CancellationToken cancellationToken)
        => (await paymentService.GetMyPaymentsAsync(User.GetUserId(), query, this.CurrentUrlWithFilters(), cancellationToken)).ToActionResult(this);

    [HttpGet("top-ups/{id:guid}")]
    public async Task<IActionResult> GetTopUp(Guid id, CancellationToken cancellationToken)
        => (await paymentService.GetMyPaymentAsync(User.GetUserId(), id, cancellationToken)).ToActionResult(this);
}
