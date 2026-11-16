namespace ELearningPlatform.Application.Abstractions.Services;

/// <summary>Queues outgoing email/SMS as background jobs and returns the job id.</summary>
public interface INotificationService
{
    Task<string> SendEmailAsync(SendEmailDto dto, CancellationToken cancellationToken = default);
    string SendSMS(SendSMSDto dto);
}
