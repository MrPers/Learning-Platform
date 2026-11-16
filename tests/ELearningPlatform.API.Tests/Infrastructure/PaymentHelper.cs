using System.Net;
using System.Security.Cryptography;
using System.Text;
using ELearningPlatform.Application.DTOs.Payments;

namespace ELearningPlatform.API.Tests.Infrastructure;

/// <summary>Money-related calls: idempotent POSTs, top-ups completed by a correctly signed Stripe webhook.</summary>
public static class PaymentHelper
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static Task<HttpResponseMessage> PostIdempotentAsync<T>(this HttpClient client, string url, T body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = System.Net.Http.Json.JsonContent.Create(body, options: Json.Options) };
        request.Headers.Add(IdempotencyHeader, key ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PostIdempotentAsync(this HttpClient client, string url, string? key = null)
        => client.PostIdempotentAsync<object?>(url, null, key);

    public static async Task<TopUpDto> StartTopUpAsync(this TestUser user, decimal amount)
    {
        var response = await user.Client.PostIdempotentAsync("api/Wallet/top-ups", new { amount });
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        return await response.ReadAsync<TopUpDto>();
    }

    /// <summary>Starts a top-up and completes it the way Stripe does: a signed payment_intent.succeeded webhook.</summary>
    public static async Task<TopUpDto> TopUpAsync(this ApiFactory factory, TestUser user, decimal amount)
    {
        var topUp = await user.StartTopUpAsync(amount);
        var response = await factory.SendStripeEventAsync("payment_intent.succeeded", PaymentIntentJson(topUp.PaymentId, amount));
        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
        return topUp;
    }

    public static string PaymentIntentJson(Guid paymentId, decimal amount, string currency = "usd")
        => $$"""{"id":"pi_{{paymentId:N}}","object":"payment_intent","amount":{{(long)(amount * 100)}},"amount_received":{{(long)(amount * 100)}},"currency":"{{currency}}"}""";

    public static string EventJson(string type, string dataObject, string? eventId = null)
        => $$"""{"id":"{{eventId ?? "evt_" + Guid.NewGuid().ToString("N")}}","object":"event","api_version":"2025-01-27.acacia","type":"{{type}}","data":{"object":""" + dataObject + "}}";

    public static Task<HttpResponseMessage> SendStripeEventAsync(this ApiFactory factory, string type, string dataObject, string? eventId = null, string? secret = null)
        => factory.SendStripeWebhookAsync(EventJson(type, dataObject, eventId), secret);

    public static Task<HttpResponseMessage> SendStripeWebhookAsync(this ApiFactory factory, string payload, string? secret = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret ?? ApiFactory.StripeWebhookSecret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var request = new HttpRequestMessage(HttpMethod.Post, "api/Payments/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={Convert.ToHexStringLower(hash)}");
        return factory.CreateHttpsClient(handleCookies: false).SendAsync(request);
    }

    public static async Task<WalletDto> GetWalletAsync(this TestUser user)
    {
        var response = await user.Client.GetAsync("api/Wallet");
        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
        return await response.ReadAsync<WalletDto>();
    }
}
