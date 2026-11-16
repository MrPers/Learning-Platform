using System.Text.Json.Serialization;

namespace ELearningPlatform.Application.DTOs.Authentication;

public class AuthDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    /// <summary>Access token expiry (UTC).</summary>
    public DateTime ExpiresOn { get; set; }

    /// <summary>Raw refresh token. Sent to the client only as an HttpOnly cookie, never in the JSON body.</summary>
    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiration { get; set; }
}
