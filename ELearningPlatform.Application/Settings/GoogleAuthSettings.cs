using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

public class GoogleAuthSettings : IValidatableObject
{
    /// <summary>OAuth client ids whose id_tokens are accepted (the token "aud" claim must be one of them).</summary>
    [Required, MinLength(1)]
    public List<string> ClientIds { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClientIds.Any(string.IsNullOrWhiteSpace))
            yield return new ValidationResult("GoogleAuth:ClientIds must not contain empty values.", [nameof(ClientIds)]);
    }
}
