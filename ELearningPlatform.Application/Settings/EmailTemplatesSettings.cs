using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

public class EmailTemplatesSettings
{
    [Required]
    public string WelcomePath { get; set; } = string.Empty;

    [Required]
    public string ConfirmEmailPath { get; set; } = string.Empty;

    [Required]
    public string ChangePasswordPath { get; set; } = string.Empty;

    [Required]
    public string ResetPasswordPath { get; set; } = string.Empty;

    [Required]
    public string TwoFactorCodePath { get; set; } = string.Empty;
}
