namespace ELearningPlatform.Application.DTOs.Notification;

public class SendSMSDto
{
    public string ToPhoneNumber { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
