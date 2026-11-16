using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace ELearningPlatform.Infrastructure.Services.Payments;

/// <summary>
/// Stripe PaymentIntents and Refunds. Every call carries an idempotency key, so a retried call never
/// charges or refunds twice. Stripe's error messages are logged; keys, client secrets and amounts are not.
/// </summary>
public sealed class StripePaymentGateway(IStripeClient client, ILogger<StripePaymentGateway> logger) : IPaymentGateway
{
    public async Task<ProviderPaymentIntent> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = Money.ToMinorUnits(request.Amount),
            Currency = request.Currency.ToLowerInvariant(),
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
            Description = "Wallet top-up",
            Metadata = new Dictionary<string, string>
            {
                ["payment_id"] = request.PaymentId.ToString(),
                ["user_id"] = request.UserId,
            },
        };

        try
        {
            var intent = await new PaymentIntentService(client)
                .CreateAsync(options, new RequestOptions { IdempotencyKey = request.IdempotencyKey }, cancellationToken);
            return new ProviderPaymentIntent(intent.Id, intent.ClientSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Stripe rejected PaymentIntent for payment {PaymentId}: {Type} {Code}", request.PaymentId, ex.StripeError?.Type, ex.StripeError?.Code);
            throw new PaymentProviderException($"Stripe error: {ex.StripeError?.Code ?? ex.HttpStatusCode.ToString()}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PaymentProviderException("Stripe could not be reached.", ex);
        }
    }

    public async Task<ProviderRefund> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        var options = new RefundCreateOptions
        {
            PaymentIntent = request.ProviderPaymentId,
            Amount = Money.ToMinorUnits(request.Amount),
            Metadata = new Dictionary<string, string> { ["payment_id"] = request.PaymentId.ToString() },
        };

        try
        {
            var refund = await new RefundService(client)
                .CreateAsync(options, new RequestOptions { IdempotencyKey = request.IdempotencyKey }, cancellationToken);
            return new ProviderRefund(refund.Id, refund.Status);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Stripe rejected refund for payment {PaymentId}: {Type} {Code}", request.PaymentId, ex.StripeError?.Type, ex.StripeError?.Code);
            throw new PaymentProviderException($"Stripe error: {ex.StripeError?.Code ?? ex.HttpStatusCode.ToString()}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PaymentProviderException("Stripe could not be reached.", ex);
        }
    }
}

/// <summary>Verifies the Stripe-Signature header (HMAC-SHA256 with the endpoint secret, timestamp tolerance) and maps events.</summary>
public sealed class StripeWebhookParser(IOptions<StripeSettings> settings, TimeProvider timeProvider, ILogger<StripeWebhookParser> logger) : IPaymentWebhookParser
{
    public bool TryParse(string payload, string? signatureHeader, out PaymentWebhookEvent? webhookEvent)
    {
        webhookEvent = null;
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, settings.Value.WebhookSecret,
                tolerance: settings.Value.WebhookToleranceSeconds,
                utcNow: timeProvider.GetUtcNow().ToUnixTimeSeconds(),
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Rejected Stripe webhook: {Reason}", ex.Message);
            return false;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            logger.LogWarning("Rejected Stripe webhook: malformed payload");
            return false;
        }

        webhookEvent = stripeEvent.Data?.Object switch
        {
            PaymentIntent intent when stripeEvent.Type == EventTypes.PaymentIntentSucceeded => new PaymentWebhookEvent(
                stripeEvent.Id, stripeEvent.Type, PaymentWebhookKind.PaymentSucceeded, intent.Id,
                Money.FromMinorUnits(intent.AmountReceived), intent.Currency?.ToUpperInvariant()),

            PaymentIntent intent when stripeEvent.Type == EventTypes.PaymentIntentPaymentFailed => new PaymentWebhookEvent(
                stripeEvent.Id, stripeEvent.Type, PaymentWebhookKind.PaymentFailed, intent.Id,
                Money.FromMinorUnits(intent.Amount), intent.Currency?.ToUpperInvariant(), intent.LastPaymentError?.Message),

            Charge charge when stripeEvent.Type == EventTypes.ChargeRefunded && charge.PaymentIntentId != null => new PaymentWebhookEvent(
                stripeEvent.Id, stripeEvent.Type, PaymentWebhookKind.PaymentRefunded, charge.PaymentIntentId,
                Money.FromMinorUnits(charge.AmountRefunded), charge.Currency?.ToUpperInvariant()),

            _ => new PaymentWebhookEvent(stripeEvent.Id, stripeEvent.Type, PaymentWebhookKind.Ignored),
        };
        return true;
    }
}
