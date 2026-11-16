using System.Net;
using Hangfire;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Infrastructure.Services;

public class AppEmailService(
    IBackgroundJobClient backgroundJobClient,
    IOptions<EmailTemplatesSettings> templates,
    IOptions<FrontendSettings> frontend) : IAppEmailService
{
    private readonly EmailTemplatesSettings _templates = templates.Value;
    private readonly FrontendSettings _frontend = frontend.Value;

    public async Task SendConfirmEmailAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
    {
        var link = BuildLink(_frontend.ConfirmEmailPath, user.Email!, token);
        var body = (await ReadTemplateAsync(_templates.ConfirmEmailPath, cancellationToken))
            .Replace("{firstName}", Html(user.FirstName))
            .Replace("{confirmationLink}", Html(link))
            .Replace("{token}", Html(token));

        Enqueue(user, "Confirm Email", body);
    }

    public async Task SendResetPasswordEmailAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
    {
        var link = BuildLink(_frontend.ResetPasswordPath, user.Email!, token);
        var body = (await ReadTemplateAsync(_templates.ResetPasswordPath, cancellationToken))
            .Replace("{resetLink}", Html(link))
            .Replace("{token}", Html(token));

        Enqueue(user, "Reset Password", body);
    }

    public async Task SendTwoFactorEmailAsync(ApplicationUser user, string code, CancellationToken cancellationToken = default)
    {
        var body = (await ReadTemplateAsync(_templates.TwoFactorCodePath, cancellationToken))
            .Replace("{name}", Html($"{user.FirstName} {user.LastName}"))
            .Replace("{code}", Html(code));

        Enqueue(user, "Two-Factor Authentication Code", body);
    }

    public async Task SendWelcomeEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var body = (await ReadTemplateAsync(_templates.WelcomePath, cancellationToken))
            .Replace("{firstName}", Html(user.FirstName));

        Enqueue(user, "Welcome To E-Learning Platform", body);
    }

    public async Task SendPasswordChangedEmailAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var body = (await ReadTemplateAsync(_templates.ChangePasswordPath, cancellationToken))
            .Replace("{UserName}", Html($"{user.FirstName} {user.LastName}"));

        Enqueue(user, "Password Changed Successfully", body);
    }

    internal string BuildLink(string path, string email, string token)
        => $"{_frontend.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

    private void Enqueue(ApplicationUser user, string subject, string body)
        => backgroundJobClient.Enqueue<IMailService>(mail => mail.SendEmailAsync(user.Email!, subject, body, null, CancellationToken.None));

    // User-controlled values (names) go into HTML, so they're encoded to prevent markup injection
    private static string Html(string value) => WebUtility.HtmlEncode(value);

    // Template paths are relative to the app's output folder, where the .html files are copied on build
    private static Task<string> ReadTemplateAsync(string path, CancellationToken cancellationToken)
        => File.ReadAllTextAsync(Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path), cancellationToken);
}
