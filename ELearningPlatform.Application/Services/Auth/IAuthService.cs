namespace ELearningPlatform.Application.Services.Auth;

public interface IAuthService
{
    Task<Result<AuthDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<AuthDto>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result> RevokeTokenAsync(string userId, string refreshToken, CancellationToken cancellationToken = default);
    Task<Result> EnableTwoFactorAuthenticationAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result> DisableTwoFactorAuthenticationAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result> SendTwoFactorCodeAsync(TwoFactorSendCodeDto dto, CancellationToken cancellationToken = default);
    Task<Result<AuthDto>> VerifyTwoFactorAuthenticationAsync(VerifyTwoFactorDto dto, CancellationToken cancellationToken = default);
}
