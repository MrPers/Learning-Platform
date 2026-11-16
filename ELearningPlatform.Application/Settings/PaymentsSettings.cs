using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

/// <summary>Money rules of the platform. All prices, wallets and payments use the single <see cref="Currency"/>.</summary>
public class PaymentsSettings : IValidatableObject
{
    /// <summary>ISO 4217 code, e.g. "USD".</summary>
    [Required, RegularExpression("^[A-Za-z]{3}$")]
    public string Currency { get; set; } = "USD";

    /// <summary>Commission the platform keeps from every course sale, in percent.</summary>
    [Range(typeof(decimal), "0", "100")]
    public decimal PlatformFeePercent { get; set; } = 20m;

    [Range(typeof(decimal), "0.5", "1000000")]
    public decimal MinTopUp { get; set; } = 1m;

    [Range(typeof(decimal), "0.5", "1000000")]
    public decimal MaxTopUp { get; set; } = 10_000m;

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal MinPayout { get; set; } = 10m;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinTopUp > MaxTopUp)
            yield return new ValidationResult("Payments:MinTopUp must not exceed Payments:MaxTopUp.", [nameof(MinTopUp), nameof(MaxTopUp)]);
    }
}
