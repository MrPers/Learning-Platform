using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.DTOs.User;

public class UpdatePictureProfileDto
{
    public IFormFile PictureFile { get; set; } = null!;
}
