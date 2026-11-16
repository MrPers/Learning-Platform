using Microsoft.AspNetCore.Identity;
namespace ELearningPlatform.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public string? ProfilePictureUrl { get; set; } = string.Empty;

    public Student Student { get; set; } = null!;
    public Teacher Teacher { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<VideoComment> VideoComments { get; set; } = [];
    public ICollection<VideoReaction> VideoReactions { get; set; } = [];
    public ICollection<CourseComment> CourseComments { get; set; } = [];
    public ICollection<CourseReaction> CourseReactions { get; set; } = [];


}
