using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.DTOs.Media;

public class UpdateVideoFileDto
{
    public IFormFile VideoFile { get; set; } = null!;
}
