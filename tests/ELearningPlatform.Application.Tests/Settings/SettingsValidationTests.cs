using System.ComponentModel.DataAnnotations;

namespace ELearningPlatform.Application.Tests.Settings;

/// <summary>The same DataAnnotations checks ValidateOnStart runs, so a missing secret fails at startup.</summary>
public class SettingsValidationTests
{
    private static List<ValidationResult> Validate(object settings)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true);
        return results;
    }

    private static JWTSettings Jwt(string key = "0123456789abcdef0123456789abcdef") => new()
    {
        SecretKey = key, Issuer = "i", Audience = "a", AccessTokenDurationInMinutes = 15, RefreshTokenDurationInDays = 7,
    };

    [Fact]
    public void Valid_jwt_settings_pass() => Assert.Empty(Validate(Jwt()));

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("0123456789abcdef0123456789abcde")]
    public void Jwt_key_must_be_at_least_32_chars(string key)
    {
        Assert.Contains(Validate(Jwt(key)), r => r.MemberNames.Contains(nameof(JWTSettings.SecretKey)));
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(15, 0)]
    [InlineData(2000, 7)]
    [InlineData(15, 400)]
    public void Jwt_durations_must_be_in_range(double minutes, int days)
    {
        var settings = Jwt();
        settings.AccessTokenDurationInMinutes = minutes;
        settings.RefreshTokenDurationInDays = days;

        Assert.NotEmpty(Validate(settings));
    }

    [Fact]
    public void Empty_mail_settings_fail_on_every_secret()
    {
        var names = Validate(new MailSettings()).SelectMany(r => r.MemberNames).ToList();

        Assert.Contains(nameof(MailSettings.Email), names);
        Assert.Contains(nameof(MailSettings.Password), names);
        Assert.Contains(nameof(MailSettings.Host), names);
        Assert.Contains(nameof(MailSettings.Port), names);
    }

    [Fact]
    public void Valid_mail_settings_pass()
        => Assert.Empty(Validate(new MailSettings { Email = "a@b.com", DisplayName = "d", Password = "p", Host = "h", Port = 587 }));

    [Theory]
    [InlineData("+15551234567", true)]
    [InlineData("+201012345678", true)]
    [InlineData("15551234567", false)]
    [InlineData("+0123", false)]
    [InlineData("", false)]
    public void Twilio_number_must_be_e164(string number, bool valid)
    {
        var settings = new TwilioSettings { AccountSID = "AC", AuthToken = "t", TwilioPhoneNumber = number };

        Assert.Equal(valid, Validate(settings).Count == 0);
    }

    [Fact]
    public void Twilio_credentials_are_required()
    {
        var names = Validate(new TwilioSettings { TwilioPhoneNumber = "+15551234567" }).SelectMany(r => r.MemberNames).ToList();

        Assert.Contains(nameof(TwilioSettings.AccountSID), names);
        Assert.Contains(nameof(TwilioSettings.AuthToken), names);
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "" }, false)]
    [InlineData(new[] { "ok.apps.googleusercontent.com", " " }, false)]
    [InlineData(new[] { "ok.apps.googleusercontent.com" }, true)]
    public void Google_client_ids(string[] ids, bool valid)
    {
        Assert.Equal(valid, Validate(new GoogleAuthSettings { ClientIds = ids.ToList() }).Count == 0);
    }

    [Theory]
    [InlineData("admin@x.com", "Adm1n!Pass", true)]
    [InlineData("", "Adm1n!Pass", false)]
    [InlineData("not-email", "Adm1n!Pass", false)]
    [InlineData("admin@x.com", "", false)]
    [InlineData("admin@x.com", "short", false)]
    public void Seed_settings(string email, string password, bool valid)
    {
        Assert.Equal(valid, Validate(new SeedSettings { AdminEmail = email, AdminPassword = password }).Count == 0);
    }

    [Theory]
    [InlineData("https://front.test", true)]
    [InlineData("", false)]
    [InlineData("not a url", false)]
    public void Frontend_base_url(string url, bool valid)
    {
        Assert.Equal(valid, Validate(new FrontendSettings { BaseUrl = url }).Count == 0);
    }

    [Fact]
    public void Uploads_need_extensions_and_sizes()
    {
        Assert.NotEmpty(Validate(new UploadsSettings()));
        Assert.Empty(Validate(new UploadsSettings
        {
            ImageSizeInMB = 1, VideoSizeInMB = 1, FileSizeInMB = 1, ImageExtentionAllowed = [".png"], VideoExtentionAllowed = [".mp4"],
        }));
    }

    [Fact]
    public void Email_template_paths_are_required()
    {
        Assert.Equal(5, Validate(new EmailTemplatesSettings()).Count);
    }
}
