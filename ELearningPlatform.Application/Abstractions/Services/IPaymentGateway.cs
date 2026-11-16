namespace ELearningPlatform.Application.Abstractions.Services;

/// <summary>Outgoing calls to the payment provider (Stripe).</summary>
public interface IPaymentGateway
{
    /// <summary>Creates a payment the client then confirms with the provider's SDK (Stripe PaymentIntent).</summary>
    /// <exception cref="PaymentProviderException">The provider rejected the call or could not be reached.</exception>
    Task<ProviderPaymentIntent> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default);

    /// <exception cref="PaymentProviderException">The provider rejected the call or could not be reached.</exception>
    Task<ProviderRefund> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default);
}

/// <param name="IdempotencyKey">Sent to the provider, so a retried call never creates a second charge.</param>
public sealed record CreatePaymentIntentRequest(Guid PaymentId, string UserId, decimal Amount, string Currency, string IdempotencyKey);

/// <param name="ClientSecret">Lets the browser confirm the payment. Returned to the payer only; never stored or logged.</param>
public sealed record ProviderPaymentIntent(string ProviderPaymentId, string ClientSecret);

public sealed record RefundRequest(Guid PaymentId, string ProviderPaymentId, decimal Amount, string Currency, string IdempotencyKey);

public sealed record ProviderRefund(string ProviderRefundId, string Status);

/// <summary>The provider call failed. The message is safe to log (no keys, card data or amounts).</summary>
public sealed class PaymentProviderException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Incoming provider events (webhooks).</summary>
public interface IPaymentWebhookParser
{
    /// <summary>
    /// Verifies the signature and reads the event. Returns false when the signature or payload is invalid;
    /// a valid event of a type the platform doesn't handle has <see cref="PaymentWebhookKind.Ignored"/>.
    /// </summary>
    bool TryParse(string payload, string? signatureHeader, out PaymentWebhookEvent? webhookEvent);
}

public enum PaymentWebhookKind
{
    Ignored = 0,
    PaymentSucceeded,
    PaymentFailed,
    PaymentRefunded,
}

/// <param name="Amount">Amount received (succeeded) or refunded (refunded), in major units.</param>
public sealed record PaymentWebhookEvent(
    string EventId,
    string Type,
    PaymentWebhookKind Kind,
    string? ProviderPaymentId = null,
    decimal? Amount = null,
    string? Currency = null,
    string? FailureReason = null);
