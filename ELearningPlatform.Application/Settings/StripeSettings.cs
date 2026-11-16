using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

/// <summary>Stripe API keys (use test-mode keys: sk_test_/pk_test_). Secrets: never logged, never returned except the publishable key.</summary>
public class StripeSettings
{
    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Public key the frontend needs for Stripe.js.</summary>
    [Required]
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>Signing secret of the webhook endpoint ("whsec_..."), used to verify the Stripe-Signature header.</summary>
    [Required]
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Maximum age of a webhook signature, in seconds (replay protection).</summary>
    [Range(1, 3600)]
    public int WebhookToleranceSeconds { get; set; } = 300;
}
