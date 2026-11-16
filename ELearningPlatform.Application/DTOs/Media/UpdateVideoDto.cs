namespace ELearningPlatform.Application.DTOs.Media;

public class UpdateVideoDto
{
    public string? Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? OriginalName { get; set; } = string.Empty;
}
