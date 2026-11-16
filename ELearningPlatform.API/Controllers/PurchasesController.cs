namespace ELearningPlatform.API.Controllers;

/// <summary>The caller's course purchases. Buying is POST api/Courses/{id}/purchase.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PoliciesConstants.StudentOnly)]
public class PurchasesController(IPurchaseService purchaseService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] PurchaseQuery query, CancellationToken cancellationToken)
        => (await purchaseService.GetMyPurchasesAsync(User.GetUserId(), query, this.CurrentUrlWithFilters(), cancellationToken)).ToActionResult(this);
}
