namespace ELearningPlatform.Application.Abstractions.Services;

public interface ISMSService
{
    /// <summary>Sends one SMS. Runs as a background job and throws on failure so the job is retried.</summary>
    Task SendSMSAsync(string phoneNumber, string body, CancellationToken cancellationToken = default);
}
