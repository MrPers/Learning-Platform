using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.DTOs.Authentication;

/// <summary>Changes the password of the signed-in user (taken from the access token, not the body).</summary>
public class ChangePasswordDto
{
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}
