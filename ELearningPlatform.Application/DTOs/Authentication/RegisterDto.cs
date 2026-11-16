using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
namespace ELearningPlatform.Application.DTOs.Authentication;

public class RegisterDto
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string Email { get; set; } = null!;
    public Gender Gender { get; set; }
    public IFormFile? ProfilePictureFile { get; set; }
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = null!;
}
