namespace ELearningPlatform.Application.DTOs.Authentication;

public class TwoFactorSendCodeDto
{
    public string Email { get; set; } = string.Empty;
    public TwoFactorMethod Method { get; set; }

}
