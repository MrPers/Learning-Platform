namespace ELearningPlatform.Domain.Entities;

public class VideoReaction
{
    public int Id { get; set; }
    public DateTime ReactedAt { get; set; }
    public bool IsLike { get; set; } // true for like, false for dislike

    public int VideoId { get; set; }
    public Video Video { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;


}
