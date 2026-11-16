namespace ELearningPlatform.Application.Tests.TestInfrastructure;

/// <summary>A complete, valid configuration (test values only).</summary>
public static class TestConfiguration
{
    public const string JwtKey = "test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    public static Dictionary<string, string?> Values(string connectionString) => new()
    {
        ["ConnectionStrings:DefaultConnection"] = connectionString,
        ["Hangfire:Storage"] = "InMemory",
        ["JWT:SecretKey"] = JwtKey,
        ["JWT:Issuer"] = "https://issuer.test/",
        ["JWT:Audience"] = "https://audience.test/",
        ["JWT:AccessTokenDurationInMinutes"] = "15",
        ["JWT:RefreshTokenDurationInDays"] = "7",
        ["MailSettings:Email"] = "noreply@test.com",
        ["MailSettings:DisplayName"] = "Test",
        ["MailSettings:Password"] = "smtp-test-password",
        ["MailSettings:Host"] = "smtp.test",
        ["MailSettings:Port"] = "587",
        ["Twilio:AccountSID"] = "ACtest",
        ["Twilio:AuthToken"] = "twilio-test-token",
        ["Twilio:TwilioPhoneNumber"] = "+15550001111",
        ["GoogleAuth:ClientIds:0"] = "client.apps.googleusercontent.com",
        ["Seed:AdminEmail"] = "admin@test.com",
        ["Seed:AdminPassword"] = "Adm1n!Test",
        ["Frontend:BaseUrl"] = "https://front.test",
        ["Uploads:ImageSizeInMB"] = "1",
        ["Uploads:VideoSizeInMB"] = "2",
        ["Uploads:FileSizeInMB"] = "1",
        ["Uploads:ImageExtentionAllowed:0"] = ".png",
        ["Uploads:ImageExtentionAllowed:1"] = ".jpg",
        ["Uploads:VideoExtentionAllowed:0"] = ".mp4",
        ["EmailTemplates:WelcomePath"] = "w.html",
        ["EmailTemplates:ConfirmEmailPath"] = "c.html",
        ["EmailTemplates:ChangePasswordPath"] = "p.html",
        ["EmailTemplates:ResetPasswordPath"] = "r.html",
        ["EmailTemplates:TwoFactorCodePath"] = "t.html",
        ["Stripe:SecretKey"] = "sk_test_app_tests",
        ["Stripe:PublishableKey"] = "pk_test_app_tests",
        ["Stripe:WebhookSecret"] = "whsec_app_tests",
        ["Payments:Currency"] = "USD",
        ["Payments:PlatformFeePercent"] = "20",
        ["Payments:MinPayout"] = "10",
    };
}
