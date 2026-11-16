namespace ELearningPlatform.Application.Services.Auth;

public interface IRegistrationService
{
    Task<Result> RegisterTeacherAsync(RegisterTeacherDto dto, CancellationToken cancellationToken = default);
    Task<Result> RegisterStudentAsync(RegisterStudentDto dto, CancellationToken cancellationToken = default);
    Task<Result<AuthDto>> ConfirmEmailAsync(string email, string token, CancellationToken cancellationToken = default);
    Task<Result> ResendEmailConfirmationAsync(ResendConfirmEmailDto dto, CancellationToken cancellationToken = default);
}
