namespace ELearningPlatform.Domain.Entities;

public class CourseReaction
{
    public int Id { get; set; }
    public bool IsLike { get; set; }
    public Rating? Rating { get; set; }
    public DateTime ReactedAt { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
