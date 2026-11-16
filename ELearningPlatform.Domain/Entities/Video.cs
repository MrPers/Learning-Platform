namespace ELearningPlatform.Domain.Entities;

public class Video
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public List<VideoComment> VideoComments { get; set; } = [];
    public List<VideoReaction> VideoReactions { get; set; } = [];
}
