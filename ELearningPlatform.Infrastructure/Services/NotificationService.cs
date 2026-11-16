using Hangfire;
using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Infrastructure.Services;

public class NotificationService(IBackgroundJobClient backgroundJobClient) : INotificationService
{
    public async Task<string> SendEmailAsync(SendEmailDto dto, CancellationToken cancellationToken = default)
    {
        // Copy uploads out of the request now: the job runs later, after the request stream is gone
        var attachments = dto.Attachments is { Count: > 0 }
            ? await ToAttachmentsAsync(dto.Attachments, cancellationToken)
            : null;

        return backgroundJobClient.Enqueue<IMailService>(mail =>
            mail.SendEmailAsync(dto.ToEmail, dto.Subject, dto.Body, attachments, CancellationToken.None));
    }

    public string SendSMS(SendSMSDto dto)
        => backgroundJobClient.Enqueue<ISMSService>(sms =>
            sms.SendSMSAsync(dto.ToPhoneNumber, dto.Body, CancellationToken.None));

    internal static async Task<List<EmailAttachment>> ToAttachmentsAsync(IEnumerable<IFormFile> files, CancellationToken cancellationToken)
    {
        var attachments = new List<EmailAttachment>();
        foreach (var file in files.Where(f => f.Length > 0))
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            attachments.Add(new EmailAttachment(Path.GetFileName(file.FileName), contentType, ms.ToArray()));
        }
        return attachments;
    }
}
