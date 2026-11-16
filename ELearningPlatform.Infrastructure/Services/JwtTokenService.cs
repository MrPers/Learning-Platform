using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ELearningPlatform.Infrastructure.Services;

public class JwtTokenService(UserManager<ApplicationUser> userManager, IOptions<JWTSettings> jwtSettings, TimeProvider timeProvider) : IJwtTokenService
{
    /// <summary>Claim names used in access tokens (short JWT names; the API disables inbound claim mapping).</summary>
    public const string RoleClaim = "role";

    private static readonly JsonWebTokenHandler TokenHandler = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly JWTSettings _jwtSettings = jwtSettings.Value;

    public async Task<JwtTokenDto> GenerateTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var roles = await userManager.GetRolesAsync(user);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_jwtSettings.AccessTokenDurationInMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id,
                [JwtRegisteredClaimNames.Email] = user.Email ?? string.Empty,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [RoleClaim] = roles.ToArray(),
            },
            SigningCredentials = new SigningCredentials(CreateSigningKey(_jwtSettings.SecretKey), SecurityAlgorithms.HmacSha256),
        };

        return new JwtTokenDto
        {
            AccessToken = TokenHandler.CreateToken(descriptor),
            ExpiresAt = expires,
        };
    }

    public IssuedRefreshToken GenerateRefreshToken(ApplicationUser user)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var entity = new RefreshToken
        {
            ApplicationUserId = user.Id,
            CreatedAt = now,
            ExpiresOn = now.AddDays(_jwtSettings.RefreshTokenDurationInDays),
            TokenHash = HashRefreshToken(token),
        };
        return new IssuedRefreshToken(entity, token);
    }

    public string HashRefreshToken(string refreshToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    public static SymmetricSecurityKey CreateSigningKey(string secretKey) => new(Encoding.UTF8.GetBytes(secretKey));
}
