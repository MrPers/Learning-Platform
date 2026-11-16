using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

public class MailSettings
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; }
}
