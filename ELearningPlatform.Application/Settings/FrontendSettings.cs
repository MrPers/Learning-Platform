using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

/// <summary>Where links in emails point to.</summary>
public class FrontendSettings
{
    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string ConfirmEmailPath { get; set; } = "/confirm-email";

    [Required]
    public string ResetPasswordPath { get; set; } = "/reset-password";
}
