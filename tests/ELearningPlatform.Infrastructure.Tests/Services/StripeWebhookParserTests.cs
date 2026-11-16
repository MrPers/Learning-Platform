using System.Security.Cryptography;
using System.Text;
using ELearningPlatform.Infrastructure.Services.Payments;

namespace ELearningPlatform.Infrastructure.Tests.Services;

public class StripeWebhookParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _time = new(Now);

    private StripeWebhookParser CreateParser() => new(
        Options.Create(new StripeSettings { SecretKey = "sk", PublishableKey = "pk", WebhookSecret = TestSettings.StripeWebhookSecret, WebhookToleranceSeconds = 300 }),
        _time, NullLogger<StripeWebhookParser>.Instance);

    /// <summary>Stripe's scheme: "t={unix},v1={hex HMAC-SHA256(secret, "{t}.{payload}")}".</summary>
    public static string Sign(string payload, string secret, DateTimeOffset at)
    {
        var timestamp = at.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    private static string Event(string type, string dataObject, string id = "evt_123")
        => $$"""{"id":"{{id}}","object":"event","api_version":"2025-01-27.acacia","type":"{{type}}","data":{"object":""" + dataObject + "}}";

    private const string SucceededIntent = """{"id":"pi_1","object":"payment_intent","amount":3050,"amount_received":3050,"currency":"usd","status":"succeeded"}""";

    [Fact]
    public void Valid_signature_and_succeeded_event_is_mapped()
    {
        var payload = Event("payment_intent.succeeded", SucceededIntent);

        var ok = CreateParser().TryParse(payload, Sign(payload, TestSettings.StripeWebhookSecret, Now), out var evt);

        Assert.True(ok);
        Assert.Equal(new PaymentWebhookEvent("evt_123", "payment_intent.succeeded", PaymentWebhookKind.PaymentSucceeded, "pi_1", 30.50m, "USD"), evt);
    }

    [Fact]
    public void Wrong_secret_is_rejected()
    {
        var payload = Event("payment_intent.succeeded", SucceededIntent);

        Assert.False(CreateParser().TryParse(payload, Sign(payload, "whsec_attacker", Now), out var evt));
        Assert.Null(evt);
    }

    [Fact]
    public void Tampered_payload_is_rejected()
    {
        var payload = Event("payment_intent.succeeded", SucceededIntent);
        var signature = Sign(payload, TestSettings.StripeWebhookSecret, Now);

        Assert.False(CreateParser().TryParse(payload.Replace("3050", "999999"), signature, out _));
    }

    [Fact]
    public void Replayed_old_signature_is_rejected()
    {
        var payload = Event("payment_intent.succeeded", SucceededIntent);

        Assert.False(CreateParser().TryParse(payload, Sign(payload, TestSettings.StripeWebhookSecret, Now.AddMinutes(-6)), out _));
        Assert.True(CreateParser().TryParse(payload, Sign(payload, TestSettings.StripeWebhookSecret, Now.AddMinutes(-4)), out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void Missing_or_malformed_signature_is_rejected(string? signature)
        => Assert.False(CreateParser().TryParse(Event("payment_intent.succeeded", SucceededIntent), signature, out _));

    [Fact]
    public void Failed_payment_carries_the_reason()
    {
        var payload = Event("payment_intent.payment_failed",
            """{"id":"pi_2","object":"payment_intent","amount":1000,"currency":"usd","last_payment_error":{"message":"Your card was declined."}}""");

        Assert.True(CreateParser().TryParse(payload, Sign(payload, TestSettings.StripeWebhookSecret, Now), out var evt));
        Assert.Equal((PaymentWebhookKind.PaymentFailed, "pi_2", "Your card was declined."), (evt!.Kind, evt.ProviderPaymentId, evt.FailureReason));
    }

    [Fact]
    public void Charge_refunded_maps_to_its_payment_intent()
    {
        var payload = Event("charge.refunded",
            """{"id":"ch_1","object":"charge","amount":1000,"amount_refunded":1000,"currency":"usd","payment_intent":"pi_3"}""");

        Assert.True(CreateParser().TryParse(payload, Sign(payload, TestSettings.StripeWebhookSecret, Now), out var evt));
        Assert.Equal((PaymentWebhookKind.PaymentRefunded, "pi_3", 10m), (evt!.Kind, evt.ProviderPaymentId, evt.Amount));
    }

    [Fact]
    public void Other_event_types_are_valid_but_ignored()
    {
        var payload = Event("customer.created", """{"id":"cus_1","object":"customer"}""");

        Assert.True(CreateParser().TryParse(payload, Sign(payload, TestSettings.StripeWebhookSecret, Now), out var evt));
        Assert.Equal(PaymentWebhookKind.Ignored, evt!.Kind);
    }
}
