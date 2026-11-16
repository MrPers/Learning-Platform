namespace ELearningPlatform.Application.Abstractions.Services;

/// <summary>Builds the platform's transactional emails from templates and queues them for delivery.</summary>
public interface IAppEmailService
{
    Task SendWelcomeEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task SendConfirmEmailAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default);
    Task SendResetPasswordEmailAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default);
    Task SendTwoFactorEmailAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default);
    Task SendPasswordChangedEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
