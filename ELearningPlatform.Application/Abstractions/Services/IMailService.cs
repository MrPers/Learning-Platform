namespace ELearningPlatform.Application.Abstractions.Services;

public interface IMailService
{
    /// <summary>
    /// Sends one email. Runs as a background job, so every argument must be serializable
    /// (attachments are plain bytes, never request-bound streams). Throws on delivery failure so the job is retried.
    /// </summary>
    Task SendEmailAsync(string mailTo, string subject, string body,
        IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default);
}
