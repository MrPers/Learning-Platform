namespace ELearningPlatform.Application.DTOs.Authentication;

public class ExternalLoginDto
{
    public string IdToken { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}
