namespace ELearningPlatform.Application.DTOs.Authentication;

public class VerifyTwoFactorDto
{
    public string Email { get; set; } = string.Empty;
    public TwoFactorMethod Method { get; set; }
    public string Code { get; set; } = string.Empty;
}
