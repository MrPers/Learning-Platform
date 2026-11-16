namespace ELearningPlatform.Application.Services.Auth;

public interface IExternalLoginService
{
    Task<Result<AuthDto>> GoogleLoginAsync(ExternalLoginDto dto, CancellationToken cancellationToken = default);
}
