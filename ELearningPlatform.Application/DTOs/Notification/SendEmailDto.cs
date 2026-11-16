using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.DTOs.Notification;

public class SendEmailDto
{
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<IFormFile>? Attachments { get; set; }
}
