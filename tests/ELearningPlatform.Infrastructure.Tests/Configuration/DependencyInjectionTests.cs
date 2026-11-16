using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Twilio.Clients;
using InfrastructureDI = ELearningPlatform.Infrastructure.DependencyInjection;

namespace ELearningPlatform.Infrastructure.Tests.Configuration;

public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider(Action<Dictionary<string, string?>>? change = null)
    {
        var values = TestSettings.FullConfiguration();
        change?.Invoke(values);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Substitute.For<IWebHostEnvironment>());
        services.AddInfrastructure();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(IMailService), typeof(MailService))]
    [InlineData(typeof(ISMSService), typeof(SMSService))]
    [InlineData(typeof(INotificationService), typeof(NotificationService))]
    [InlineData(typeof(IAppEmailService), typeof(AppEmailService))]
    [InlineData(typeof(IMediaService), typeof(MediaService))]
    [InlineData(typeof(IJwtTokenService), typeof(JwtTokenService))]
    [InlineData(typeof(IGoogleTokenValidator), typeof(GoogleTokenValidator))]
    [InlineData(typeof(IPaymentGateway), typeof(ELearningPlatform.Infrastructure.Services.Payments.StripePaymentGateway))]
    [InlineData(typeof(IPaymentWebhookParser), typeof(ELearningPlatform.Infrastructure.Services.Payments.StripeWebhookParser))]
    [InlineData(typeof(ICurrentUser), typeof(HttpCurrentUser))]
    public void AddInfrastructure_ResolvesService(Type service, Type implementation)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.IsType(implementation, scope.ServiceProvider.GetRequiredService(service));
    }

    [Fact]
    public void AddInfrastructure_ResolvesIdentityAndDbContext()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void IAppDbContext_IsTheSameScopedInstanceAsAppDbContext()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.Same(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<ELearningPlatform.Application.Abstractions.Persistence.IAppDbContext>());
    }

    [Fact]
    public void TwilioClient_IsSingletonBuiltFromSettings()
    {
        using var provider = BuildProvider();

        var client = provider.GetRequiredService<ITwilioRestClient>();
        Assert.Same(client, provider.GetRequiredService<ITwilioRestClient>());
        Assert.Equal("ACtest", client.AccountSid);
    }

    [Fact]
    public void Hangfire_InMemoryStorageWhenConfigured()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IBackgroundJobClient>());
        Assert.Contains("InMemory", provider.GetRequiredService<JobStorage>().GetType().Name);
    }

    [Fact]
    public void Hangfire_DefaultsToSQLiteStorage()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hangfire-{Guid.NewGuid():N}.db");
        using var provider = BuildProvider(v => { v["Hangfire:Storage"] = null; v["ConnectionStrings:HangfireConnection"] = file; });

        Assert.Contains("SQLite", provider.GetRequiredService<JobStorage>().GetType().Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hangfire_SQLiteWithoutConnectionString_Throws()
    {
        using var provider = BuildProvider(v => { v["Hangfire:Storage"] = "SQLite"; v["ConnectionStrings:HangfireConnection"] = null; });

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<JobStorage>());
        Assert.Contains("HangfireConnection", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingDefaultConnection_ThrowsWhenContextIsResolved(string? value)
    {
        using var provider = BuildProvider(v => v["ConnectionStrings:DefaultConnection"] = value);
        using var scope = provider.CreateScope();

        var ex = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<AppDbContext>().Model);
        Assert.Contains("DefaultConnection", ex.Message);
    }

    [Fact]
    public void StartupValidation_PassesWithCompleteConfiguration()
    {
        using var provider = BuildProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    /// <summary>Marker value: delete the key instead of setting it.</summary>
    private const string Remove = "<remove>";

    public static TheoryData<string, string?> InvalidSettings => new()
    {
        { "JWT:SecretKey", null },
        { "JWT:SecretKey", "too-short-key" },
        { "JWT:Issuer", "" },
        { "JWT:Audience", "" },
        { "JWT:AccessTokenDurationInMinutes", "0" },
        { "JWT:RefreshTokenDurationInDays", "0" },
        { "JWT:RefreshTokenDurationInDays", "1000" },
        { "MailSettings:Email", "not-an-email" },
        { "MailSettings:Password", "" },
        { "MailSettings:Host", "" },
        { "MailSettings:Port", "0" },
        { "MailSettings:Port", "70000" },
        { "Twilio:AccountSID", "" },
        { "Twilio:AuthToken", "" },
        { "Twilio:TwilioPhoneNumber", "01012345678" },
        { "Twilio:TwilioPhoneNumber", "+0123" },
        { "GoogleAuth:ClientIds:0", "" },
        { "GoogleAuth:ClientIds:0", Remove },
        { "Seed:AdminEmail", "admin" },
        { "Seed:AdminPassword", "short" },
        { "Frontend:BaseUrl", "not a url" },
        { "Uploads:ImageSizeInMB", "0" },
        { "Uploads:ImageExtentionAllowed:0", Remove },
        { "Uploads:VideoExtentionAllowed:0", Remove },
        { "EmailTemplates:WelcomePath", "" },
        { "Stripe:SecretKey", Remove },
        { "Stripe:PublishableKey", "" },
        { "Stripe:WebhookSecret", Remove },
        { "Payments:Currency", "DOLLAR" },
        { "Payments:PlatformFeePercent", "101" },
        { "Payments:MinTopUp", "20000" },
    };

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void StartupValidation_FailsForInvalidSetting(string key, string? value)
    {
        using var provider = BuildProvider(v => { if (value == Remove) v.Remove(key); else v[key] = value; });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void JwtOptions_InvalidSecret_FailsWhenAccessed()
    {
        using var provider = BuildProvider(v => v["JWT:SecretKey"] = "short");

        var ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<JWTSettings>>().Value);
        Assert.Contains(nameof(JWTSettings.SecretKey), ex.Message);
    }

    [Fact]
    public void GoogleClientIds_AreBound()
    {
        using var provider = BuildProvider(v => v["GoogleAuth:ClientIds:1"] = "client-2");

        Assert.Equal(["client-1.apps.googleusercontent.com", "client-2"], provider.GetRequiredService<IOptions<GoogleAuthSettings>>().Value.ClientIds);
    }

    [Fact]
    public void Identity_LockoutAfterFiveAttemptsForFifteenMinutes()
    {
        using var provider = BuildProvider();
        var lockout = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout;

        Assert.True(lockout.AllowedForNewUsers);
        Assert.Equal(5, lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), lockout.DefaultLockoutTimeSpan);
        Assert.Equal(InfrastructureDI.MaxFailedAccessAttempts, lockout.MaxFailedAccessAttempts);
    }

    [Fact]
    public void Identity_StrongPasswordPolicy()
    {
        using var provider = BuildProvider();
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(8, options.Password.RequiredLength);
        Assert.True(options.Password.RequireDigit);
        Assert.True(options.Password.RequireLowercase);
        Assert.True(options.Password.RequireUppercase);
        Assert.True(options.Password.RequireNonAlphanumeric);
        Assert.True(options.User.RequireUniqueEmail);
    }

    [Theory]
    [InlineData("Passw0rd!", true)]
    [InlineData("Sh0rt!", false)]
    [InlineData("password1!", false)]
    [InlineData("PASSWORD1!", false)]
    [InlineData("Password!!", false)]
    [InlineData("Password12", false)]
    public async Task Identity_PasswordValidatorEnforcesPolicy(string password, bool valid)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var results = await Task.WhenAll(users.PasswordValidators.Select(v => v.ValidateAsync(users, new ApplicationUser(), password)));

        Assert.Equal(valid, results.All(r => r.Succeeded));
    }

    [Fact]
    public void Identity_DoesNotRegisterCookieAuthentication()
    {
        using var provider = BuildProvider();

        Assert.Null(provider.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>());
    }

    [Fact]
    public void Identity_HasDefaultTokenProviders()
    {
        using var provider = BuildProvider();
        var providers = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.Tokens.ProviderMap;

        Assert.Contains(TokenOptions.DefaultProvider, providers.Keys);
        Assert.Contains(TokenOptions.DefaultEmailProvider, providers.Keys);
        Assert.Contains(TokenOptions.DefaultPhoneProvider, providers.Keys);
    }
}
