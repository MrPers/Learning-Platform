namespace ELearningPlatform.Application.DTOs.Authentication;

public class JwtTokenDto
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
