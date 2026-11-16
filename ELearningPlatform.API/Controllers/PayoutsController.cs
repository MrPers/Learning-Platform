namespace ELearningPlatform.API.Controllers;

/// <summary>Teacher earnings and payout requests.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PoliciesConstants.TeacherOnly)]
public class PayoutsController(IPayoutService payoutService) : ControllerBase
{
    [HttpGet("earnings")]
    public async Task<IActionResult> GetEarnings(CancellationToken cancellationToken)
        => (await payoutService.GetEarningsAsync(User.GetUserId(), cancellationToken)).ToActionResult(this);

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] PayoutQuery query, CancellationToken cancellationToken)
        => (await payoutService.GetMyPayoutsAsync(User.GetUserId(), query, this.CurrentUrlWithFilters(), cancellationToken)).ToActionResult(this);

    /// <summary>Reserves the amount from the wallet until an admin approves or rejects the payout. Requires an Idempotency-Key header.</summary>
    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [Idempotent]
    [HttpPost]
    public async Task<IActionResult> RequestPayout(RequestPayoutDto dto, CancellationToken cancellationToken)
    {
        var result = await payoutService.RequestAsync(User.GetUserId(), dto, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Data) : result.ToProblem(this);
    }
}
