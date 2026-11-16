namespace ELearningPlatform.Infrastructure.Tests.Support;

/// <summary>Valid settings objects and a full in-memory configuration matching appsettings.json.</summary>
public static class TestSettings
{
    public const string JwtSecret = "test-secret-key-that-is-long-enough-for-hmac-sha256!";
    public const string Issuer = "https://issuer.test/";
    public const string Audience = "https://audience.test/";
    public const string StripeWebhookSecret = "whsec_infra_tests";

    public static JWTSettings Jwt(double minutes = 15, int days = 7) => new()
    {
        SecretKey = JwtSecret,
        Issuer = Issuer,
        Audience = Audience,
        AccessTokenDurationInMinutes = minutes,
        RefreshTokenDurationInDays = days,
    };

    public static MailSettings Mail() => new()
    {
        Email = "noreply@elearning.test",
        DisplayName = "E-Learning",
        Password = "smtp-password",
        Host = "smtp.test",
        Port = 587,
    };

    public static TwilioSettings Twilio() => new()
    {
        AccountSID = "ACtest",
        AuthToken = "token",
        TwilioPhoneNumber = "+15550001111",
    };

    public static UploadsSettings Uploads() => new()
    {
        ImageSizeInMB = 1,
        VideoSizeInMB = 2,
        FileSizeInMB = 1,
        ImageExtentionAllowed = [".jpg", ".jpeg", ".png"],
        VideoExtentionAllowed = [".mp4", ".webm"],
    };

    public static FrontendSettings Frontend(string baseUrl = "https://app.test") => new()
    {
        BaseUrl = baseUrl,
        ConfirmEmailPath = "/confirm-email",
        ResetPasswordPath = "/reset-password",
    };

    public static SeedSettings Seed(string adminPassword = "Admin@12345", string devPassword = "Test@12345") => new()
    {
        AdminEmail = "admin@elearning.test",
        AdminPassword = adminPassword,
        DevUsersPassword = devPassword,
    };

    /// <summary>Every setting AddInfrastructure validates, with valid values.</summary>
    public static Dictionary<string, string?> FullConfiguration() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
        ["ConnectionStrings:HangfireConnection"] = "hangfire-test.db",
        ["Hangfire:Storage"] = "InMemory",
        ["JWT:SecretKey"] = JwtSecret,
        ["JWT:Issuer"] = Issuer,
        ["JWT:Audience"] = Audience,
        ["JWT:AccessTokenDurationInMinutes"] = "15",
        ["JWT:RefreshTokenDurationInDays"] = "7",
        ["MailSettings:Email"] = "noreply@elearning.test",
        ["MailSettings:DisplayName"] = "E-Learning",
        ["MailSettings:Password"] = "smtp-password",
        ["MailSettings:Host"] = "smtp.test",
        ["MailSettings:Port"] = "587",
        ["Twilio:AccountSID"] = "ACtest",
        ["Twilio:AuthToken"] = "token",
        ["Twilio:TwilioPhoneNumber"] = "+15550001111",
        ["GoogleAuth:ClientIds:0"] = "client-1.apps.googleusercontent.com",
        ["Seed:AdminEmail"] = "admin@elearning.test",
        ["Seed:AdminPassword"] = "Admin@12345",
        ["Seed:DevUsersPassword"] = "Test@12345",
        ["Frontend:BaseUrl"] = "https://app.test",
        ["Uploads:ImageSizeInMB"] = "5",
        ["Uploads:VideoSizeInMB"] = "100",
        ["Uploads:FileSizeInMB"] = "5",
        ["Uploads:ImageExtentionAllowed:0"] = ".jpg",
        ["Uploads:VideoExtentionAllowed:0"] = ".mp4",
        ["EmailTemplates:WelcomePath"] = "EmailTemplates/Welcome.html",
        ["EmailTemplates:ConfirmEmailPath"] = "EmailTemplates/ConfirmEmail.html",
        ["EmailTemplates:ChangePasswordPath"] = "EmailTemplates/ChangePassword.html",
        ["EmailTemplates:ResetPasswordPath"] = "EmailTemplates/ResetPassword.html",
        ["EmailTemplates:TwoFactorCodePath"] = "EmailTemplates/TwoFactorCode.html",
        ["Stripe:SecretKey"] = "sk_test_infra",
        ["Stripe:PublishableKey"] = "pk_test_infra",
        ["Stripe:WebhookSecret"] = StripeWebhookSecret,
    };
}
