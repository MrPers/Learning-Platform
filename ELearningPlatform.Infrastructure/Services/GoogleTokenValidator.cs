using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Infrastructure.Services;

public partial class GoogleTokenValidator(IOptions<GoogleAuthSettings> settings, ILogger<GoogleTokenValidator> logger) : IGoogleTokenValidator
{
    private readonly GoogleJsonWebSignature.ValidationSettings _validation = new()
    {
        // Without an audience check, a token Google issued to any other app would be accepted here
        Audience = settings.Value.ClientIds,
    };

    public async Task<GoogleUser?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, _validation);
        }
        // Malformed tokens can also surface as JSON/format errors from the library; all of them mean "not valid"
        catch (Exception ex) when (ex is InvalidJwtException or Newtonsoft.Json.JsonException or FormatException or ArgumentException)
        {
            LogInvalidToken(ex.Message);
            return null;
        }

        return new GoogleUser(payload.Subject, payload.Email, payload.EmailVerified, payload.GivenName, payload.FamilyName);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected Google id_token: {Reason}")]
    private partial void LogInvalidToken(string reason);
}
