using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ELearningPlatform.Infrastructure.Services.Payments;
using Stripe;
using Twilio.Clients;

namespace ELearningPlatform.Infrastructure;

public static class DependencyInjection
{
    public const int MaxFailedAccessAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Options: bound once and validated at startup, so a missing secret fails fast instead of on first use
        services.AddValidatedOptions<MailSettings>(AppSettingsConstants.MailSettings);
        services.AddValidatedOptions<UploadsSettings>(AppSettingsConstants.Uploads);
        services.AddValidatedOptions<JWTSettings>(AppSettingsConstants.JWT);
        services.AddValidatedOptions<EmailTemplatesSettings>(AppSettingsConstants.EmailTemplates);
        services.AddValidatedOptions<TwilioSettings>(AppSettingsConstants.Twilio);
        services.AddValidatedOptions<GoogleAuthSettings>(AppSettingsConstants.GoogleAuth);
        services.AddValidatedOptions<FrontendSettings>(AppSettingsConstants.Frontend);
        services.AddValidatedOptions<SeedSettings>(AppSettingsConstants.Seed);
        services.AddValidatedOptions<PaymentsSettings>(AppSettingsConstants.Payments);
        services.AddValidatedOptions<StripeSettings>(AppSettingsConstants.Stripe);

        // Database & Identity
        // Configuration is read when the context is first built, so hosts (and tests) can still override it
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlite(RequiredConnectionString(sp.GetRequiredService<IConfiguration>(), AppSettingsConstants.DefaultConnection))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
        // Same scoped instance as UserManager uses, so both share one change tracker/transaction
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // AddIdentityCore: no cookie schemes, the API authenticates with JWT only
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = PasswordRules.MinLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        // Email-confirmation / password-reset tokens are protected with DataProtection
        services.AddDataProtection();

        // External services
        services.AddSingleton<ITwilioRestClient>(sp =>
        {
            var twilio = sp.GetRequiredService<IOptions<TwilioSettings>>().Value;
            return new TwilioRestClient(twilio.AccountSID, twilio.AuthToken);
        });
        services.AddTransient<IMailService, MailService>();
        services.AddTransient<ISMSService, SMSService>();
        services.AddTransient<INotificationService, NotificationService>();
        services.AddScoped<IAppEmailService, AppEmailService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

        // Payments (Stripe)
        services.AddSingleton<IStripeClient>(sp => new StripeClient(sp.GetRequiredService<IOptions<StripeSettings>>().Value.SecretKey));
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        services.AddSingleton<IPaymentWebhookParser, StripeWebhookParser>();

        // Background jobs
        services.AddHangfire((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            options
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();

            if (string.Equals(config[$"{AppSettingsConstants.Hangfire}:Storage"], "InMemory", StringComparison.OrdinalIgnoreCase))
                options.UseInMemoryStorage();
            else
                options.UseSQLiteStorage(RequiredConnectionString(config, AppSettingsConstants.HangfireConnection));
        });
        services.AddHangfireServer();

        return services;
    }

    private static string RequiredConnectionString(IConfiguration configuration, string name)
        => configuration.GetConnectionString(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Connection string '{name}' is not configured.");

    private static void AddValidatedOptions<TOptions>(this IServiceCollection services, string section)
        where TOptions : class
        => services.AddOptions<TOptions>()
            .BindConfiguration(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
