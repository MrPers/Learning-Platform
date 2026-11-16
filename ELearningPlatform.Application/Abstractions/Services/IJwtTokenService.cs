namespace ELearningPlatform.Application.Abstractions.Services;

public interface IJwtTokenService
{
    Task<JwtTokenDto> GenerateTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>Creates a new refresh token. Only its hash goes into the entity; the raw value is returned once.</summary>
    IssuedRefreshToken GenerateRefreshToken(ApplicationUser user);

    string HashRefreshToken(string refreshToken);
}
