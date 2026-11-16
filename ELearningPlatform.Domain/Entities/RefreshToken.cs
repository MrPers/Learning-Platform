namespace ELearningPlatform.Domain.Entities;

public class RefreshToken
{
    public int Id { get; set; }

    /// <summary>SHA-256 of the token handed to the client; the raw value is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresOn { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresOn;
    public bool IsActive(DateTime utcNow) => RevokedAt == null && !IsExpired(utcNow);

    public string ApplicationUserId { get; set; } = string.Empty;
    public ApplicationUser ApplicationUser { get; set; } = null!;
}
