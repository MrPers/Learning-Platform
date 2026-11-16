namespace ELearningPlatform.Application.DTOs.Media;

/// <summary>A stored file opened for streaming. Whoever returns it to the client disposes <see cref="Content"/>.</summary>
public class FileDownloadDto
{
    public required Stream Content { get; init; }
    public string ContentType { get; init; } = string.Empty;
    public string DownloadName { get; init; } = string.Empty;
}
