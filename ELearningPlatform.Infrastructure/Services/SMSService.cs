using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ELearningPlatform.Infrastructure.Services;

public partial class SMSService(ITwilioRestClient twilioClient, IOptions<TwilioSettings> twilio, ILogger<SMSService> logger) : ISMSService
{
    private readonly TwilioSettings _twilio = twilio.Value;

    public async Task SendSMSAsync(string phoneNumber, string body, CancellationToken cancellationToken = default)
    {
        var message = await MessageResource.CreateAsync(
            to: new PhoneNumber(phoneNumber),
            from: new PhoneNumber(_twilio.TwilioPhoneNumber),
            body: body,
            client: twilioClient);

        if (message.ErrorCode != null)
        {
            LogSmsFailed(message.Sid, message.ErrorCode, message.ErrorMessage);
            // Throwing makes Hangfire mark the job failed and retry it
            throw new InvalidOperationException($"Twilio rejected the SMS (error {message.ErrorCode}): {message.ErrorMessage}");
        }

        LogSmsSent(message.Sid);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS {Sid} sent")]
    private partial void LogSmsSent(string? sid);

    [LoggerMessage(Level = LogLevel.Error, Message = "SMS {Sid} failed with Twilio error {ErrorCode}: {ErrorMessage}")]
    private partial void LogSmsFailed(string? sid, int? errorCode, string? errorMessage);
}
