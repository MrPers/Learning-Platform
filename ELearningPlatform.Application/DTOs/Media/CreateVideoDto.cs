using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.DTOs.Media;

public class CreateVideoDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public IFormFile VideoFile { get; set; } = null!;
}
