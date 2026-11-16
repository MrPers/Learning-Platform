using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

public class TwilioSettings
{
    [Required]
    public string AccountSID { get; set; } = string.Empty;

    [Required]
    public string AuthToken { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\+[1-9]\d{6,14}$", ErrorMessage = "TwilioPhoneNumber must be in E.164 format, e.g. +15551234567.")]
    public string TwilioPhoneNumber { get; set; } = string.Empty;
}
