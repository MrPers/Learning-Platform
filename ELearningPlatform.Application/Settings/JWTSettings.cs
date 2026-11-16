using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

public class JWTSettings
{
    /// <summary>HMAC-SHA256 signing key; at least 32 characters (256 bits).</summary>
    [Required, MinLength(32)]
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 24 * 60)]
    public double AccessTokenDurationInMinutes { get; set; }

    [Range(1, 365)]
    public int RefreshTokenDurationInDays { get; set; }
}
