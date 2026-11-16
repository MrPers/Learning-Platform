using ELearningPlatform.Application.Abstractions.Services;
using ELearningPlatform.Application.DTOs.Notification;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;

namespace ELearningPlatform.API.Tests.Infrastructure;

/// <summary>
/// The real API on a TestServer: own temp SQLite database and web root per factory, in-memory Hangfire,
/// and fakes for everything that talks to the outside world (email, SMS, Google).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz-ABCDEFGHIJ";
    public const string Issuer = "https://test-issuer/";
    public const string Audience = "https://test-audience/";
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Adm1n!Passw0rd";
    public const string GoogleClientId = "test-client.apps.googleusercontent.com";
    public const string StripeWebhookSecret = "whsec_api_tests_secret";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "elearning-api-tests", Guid.NewGuid().ToString("N"));

    public FakeAppEmailService Emails { get; } = new();
    public IGoogleTokenValidator Google { get; } = Substitute.For<IGoogleTokenValidator>();
    public INotificationService Notifications { get; } = Substitute.For<INotificationService>();
    public ISMSService Sms { get; } = Substitute.For<ISMSService>();
    public IMailService Mail { get; } = Substitute.For<IMailService>();
    /// <summary>Outgoing Stripe calls. Webhooks use the real parser with <see cref="StripeWebhookSecret"/>.</summary>
    public IPaymentGateway Payments { get; } = Substitute.For<IPaymentGateway>();

    public string WebRoot => Path.Combine(_root, "wwwroot");

    public ApiFactory()
    {
        Directory.CreateDirectory(WebRoot);
        Notifications.SendSMS(Arg.Any<SendSMSDto>()).Returns("job-sms");
        Notifications.SendEmailAsync(Arg.Any<SendEmailDto>(), Arg.Any<CancellationToken>()).Returns("job-email");
        Payments.CreatePaymentIntentAsync(Arg.Any<CreatePaymentIntentRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProviderPaymentIntent($"pi_{call.Arg<CreatePaymentIntentRequest>().PaymentId:N}", "pi_secret_test"));
    }

    protected virtual IDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(_root, "app.db")}",
        ["ConnectionStrings:HangfireConnection"] = $"{Path.Combine(_root, "hangfire.db")}",
        ["Hangfire:Storage"] = "InMemory",
        ["Logging:LogLevel:Default"] = "Warning",
        ["Logging:LogLevel:Hangfire"] = "None",
        ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
        ["RateLimiting:AuthPermitLimit"] = "100000",
        ["RateLimiting:PaymentsPermitLimit"] = "100000",

        ["Stripe:SecretKey"] = "sk_test_api_tests",
        ["Stripe:PublishableKey"] = "pk_test_api_tests",
        ["Stripe:WebhookSecret"] = StripeWebhookSecret,
        ["Payments:Currency"] = "USD",
        ["Payments:PlatformFeePercent"] = "20",

        ["JWT:SecretKey"] = JwtKey,
        ["JWT:Issuer"] = Issuer,
        ["JWT:Audience"] = Audience,
        ["JWT:AccessTokenDurationInMinutes"] = "15",
        ["JWT:RefreshTokenDurationInDays"] = "7",

        ["MailSettings:Email"] = "noreply@test.local",
        ["MailSettings:DisplayName"] = "Tests",
        ["MailSettings:Password"] = "mail-password",
        ["MailSettings:Host"] = "smtp.test.local",
        ["MailSettings:Port"] = "587",

        ["Twilio:AccountSID"] = "AC00000000000000000000000000000000",
        ["Twilio:AuthToken"] = "twilio-token",
        ["Twilio:TwilioPhoneNumber"] = "+15551234567",

        ["GoogleAuth:ClientIds:0"] = GoogleClientId,

        ["Seed:AdminEmail"] = AdminEmail,
        ["Seed:AdminPassword"] = AdminPassword,
        ["Seed:DevUsersPassword"] = "Dev!Passw0rd",

        ["Frontend:BaseUrl"] = "https://frontend.test",
        ["Frontend:ConfirmEmailPath"] = "/confirm-email",
        ["Frontend:ResetPasswordPath"] = "/reset-password",

        ["Uploads:ImageSizeInMB"] = "2",
        ["Uploads:VideoSizeInMB"] = "5",
        ["Uploads:FileSizeInMB"] = "5",
        ["Uploads:ImageExtentionAllowed:0"] = ".png",
        ["Uploads:ImageExtentionAllowed:1"] = ".jpg",
        ["Uploads:ImageExtentionAllowed:2"] = ".jpeg",
        ["Uploads:ImageExtentionAllowed:3"] = ".gif",
        ["Uploads:ImageExtentionAllowed:4"] = ".webp",
        ["Uploads:VideoExtentionAllowed:0"] = ".mp4",
        ["Uploads:VideoExtentionAllowed:1"] = ".mov",
        ["Uploads:VideoExtentionAllowed:2"] = ".avi",
        ["Uploads:VideoExtentionAllowed:3"] = ".mkv",
        ["Uploads:VideoExtentionAllowed:4"] = ".webm",

        ["EmailTemplates:WelcomePath"] = "EmailTemplates/Welcome.html",
        ["EmailTemplates:ConfirmEmailPath"] = "EmailTemplates/ConfirmEmail.html",
        ["EmailTemplates:ChangePasswordPath"] = "EmailTemplates/ChangePassword.html",
        ["EmailTemplates:ResetPasswordPath"] = "EmailTemplates/ResetPassword.html",
        ["EmailTemplates:TwoFactorCodePath"] = "EmailTemplates/TwoFactorCode.html",
    };

    /// <summary>Extra service replacements for derived factories.</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAppEmailService>();
            services.AddSingleton<IAppEmailService>(Emails);
            services.RemoveAll<IGoogleTokenValidator>();
            services.AddSingleton(Google);
            services.RemoveAll<INotificationService>();
            services.AddSingleton(Notifications);
            services.RemoveAll<ISMSService>();
            services.AddSingleton(Sms);
            services.RemoveAll<IMailService>();
            services.AddSingleton(Mail);
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton(Payments);
            services.AddSingleton<IStartupFilter>(new WebRootStartupFilter(WebRoot));
            ConfigureTestServices(services);
        });
    }

    /// <summary>Client on https (so the Secure refresh cookie round-trips) that keeps cookies.</summary>
    public HttpClient CreateHttpsClient(bool handleCookies = true) => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = handleCookies,
        AllowAutoRedirect = false,
    });

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* a file may still be held briefly; it's in the temp folder */ }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    /// <summary>Points the web root (uploads) at the factory's temp folder instead of the API project.</summary>
    private sealed class WebRootStartupFilter(string webRoot) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
            env.WebRootPath = webRoot;
            env.WebRootFileProvider = new PhysicalFileProvider(webRoot);
            next(app);
        };
    }
}
