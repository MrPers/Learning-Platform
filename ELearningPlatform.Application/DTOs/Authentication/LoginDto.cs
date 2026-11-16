using System.ComponentModel.DataAnnotations;
namespace ELearningPlatform.Application.DTOs.Authentication;

public class LoginDto
{
    public string Email { get; set; } = null!;
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;
    public Role Role { get; set; }
}
