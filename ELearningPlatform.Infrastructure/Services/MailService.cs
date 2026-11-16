using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ELearningPlatform.Infrastructure.Services;

public partial class MailService(IOptions<MailSettings> mailSettings, ILogger<MailService> logger) : IMailService
{
    private readonly MailSettings _mailSettings = mailSettings.Value;

    public async Task SendEmailAsync(string mailTo, string subject, string body,
        IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default)
    {
        var email = CreateEmailMessage(mailTo, subject, body, attachments);

        using var smtp = CreateSmtpClient();
        try
        {
            await smtp.ConnectAsync(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls, cancellationToken);
            await smtp.AuthenticateAsync(_mailSettings.Email, _mailSettings.Password, cancellationToken);
            await smtp.SendAsync(email, cancellationToken);
            LogEmailSent(subject);
        }
        catch (Exception ex)
        {
            LogEmailFailed(ex, subject);
            throw; // let Hangfire retry
        }
        finally
        {
            if (smtp.IsConnected)
                await smtp.DisconnectAsync(true, CancellationToken.None);
        }
    }

    /// <summary>Seam for tests; production always talks to a real SMTP server.</summary>
    protected virtual ISmtpClient CreateSmtpClient() => new SmtpClient();

    internal MimeMessage CreateEmailMessage(string mailTo, string subject, string body, IReadOnlyList<EmailAttachment>? attachments)
    {
        var email = new MimeMessage
        {
            Sender = MailboxAddress.Parse(_mailSettings.Email),
            Subject = subject,
        };
        email.To.Add(MailboxAddress.Parse(mailTo));
        email.From.Add(new MailboxAddress(_mailSettings.DisplayName, _mailSettings.Email));

        var builder = new BodyBuilder { HtmlBody = body };
        foreach (var attachment in attachments ?? [])
            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));

        email.Body = builder.ToMessageBody();
        return email;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email \"{Subject}\" sent")]
    private partial void LogEmailSent(string subject);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending email \"{Subject}\" failed")]
    private partial void LogEmailFailed(Exception ex, string subject);
}
