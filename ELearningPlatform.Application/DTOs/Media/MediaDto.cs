namespace ELearningPlatform.Application.DTOs.Media;

public class MediaDto
{
    public string OriginalName { get; set; } = string.Empty;
    public string FakeName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
}
