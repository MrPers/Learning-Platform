namespace ELearningPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController(IPaymentService paymentService, IPaymentWebhookParser webhookParser) : ControllerBase
{
    public const string StripeSignatureHeader = "Stripe-Signature";
    public const string InvalidSignature = "Webhook.InvalidSignature";

    /// <summary>What the frontend needs to take payments: currency, limits and the Stripe publishable key.</summary>
    [Authorize(Policy = PoliciesConstants.AdminTeacherStudent)]
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(paymentService.GetConfig());

    /// <summary>
    /// Stripe webhook. Authenticated by the Stripe-Signature header (HMAC of the raw body), not by a JWT.
    /// Answers 2xx for every verified event, including duplicates and types the platform ignores, so Stripe stops retrying.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhooks/stripe")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        // The signature covers the exact bytes, so read the raw body instead of model binding
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        if (!webhookParser.TryParse(payload, Request.Headers[StripeSignatureHeader], out var webhookEvent) || webhookEvent == null)
            return Problem(detail: "Invalid webhook signature.", statusCode: StatusCodes.Status400BadRequest, title: "Bad webhook",
                extensions: new Dictionary<string, object?> { ["code"] = InvalidSignature });

        return (await paymentService.HandleWebhookAsync(webhookEvent, cancellationToken)).ToActionResult(this);
    }
}
