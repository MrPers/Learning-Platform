using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Settings;

public class SeedSettings
{
    [Required, EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>Password of the test teachers/students created in Development. Not used elsewhere.</summary>
    public string DevUsersPassword { get; set; } = string.Empty;
}
