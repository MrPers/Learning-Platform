using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ELearningPlatform.API.Tests.Infrastructure;

/// <summary>Hand-made access tokens for testing the JWT validation itself.</summary>
public static class TokenFactory
{
    public static string Create(string role, string? key = null, DateTime? expires = null, string issuer = ApiFactory.Issuer,
        string audience = ApiFactory.Audience, string subject = "00000000-0000-0000-0000-000000000001")
    {
        var exp = expires ?? DateTime.UtcNow.AddMinutes(10);
        var issued = exp.AddMinutes(-15);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = issued,
            NotBefore = issued,
            Expires = exp,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = subject,
                ["role"] = new[] { role },
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? ApiFactory.JwtKey)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }
}
