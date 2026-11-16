using System.Collections.Concurrent;
using ELearningPlatform.Application.Abstractions.Services;
using ELearningPlatform.Domain.Entities;

namespace ELearningPlatform.API.Tests.Infrastructure;

/// <summary>Records what would have been emailed, so tests can use the tokens and codes.</summary>
public sealed class FakeAppEmailService : IAppEmailService
{
    public ConcurrentDictionary<string, string> ConfirmTokens { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, string> ResetTokens { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, string> TwoFactorCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, int> WelcomeEmails { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, int> PasswordChangedEmails { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task SendWelcomeEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        WelcomeEmails.AddOrUpdate(user.Email!, 1, (_, n) => n + 1);
        return Task.CompletedTask;
    }

    public Task SendConfirmEmailAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
    {
        ConfirmTokens[user.Email!] = token;
        return Task.CompletedTask;
    }

    public Task SendResetPasswordEmailAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
    {
        ResetTokens[user.Email!] = token;
        return Task.CompletedTask;
    }

    public Task SendTwoFactorEmailAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default)
    {
        TwoFactorCodes[user.Email!] = code;
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        PasswordChangedEmails.AddOrUpdate(user.Email!, 1, (_, n) => n + 1);
        return Task.CompletedTask;
    }
}
