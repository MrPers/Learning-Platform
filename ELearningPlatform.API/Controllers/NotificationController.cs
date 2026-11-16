namespace ELearningPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PoliciesConstants.AdminOnly)]
public class NotificationController(INotificationService notificationService) : ControllerBase
{
    [HttpPost("send-sms")]
    public IActionResult SendSms(SendSMSDto dto)
        => Accepted(new JobResponse(notificationService.SendSMS(dto)));

    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmail([FromForm] SendEmailDto dto, CancellationToken cancellationToken)
        => Accepted(new JobResponse(await notificationService.SendEmailAsync(dto, cancellationToken)));
}

public sealed record JobResponse(string JobId);
