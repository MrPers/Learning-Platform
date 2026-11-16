using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace ELearningPlatform.Infrastructure.Tests.Services;

public sealed class AppEmailServiceTests : IDisposable
{
    private readonly string _dir = TestFiles.NewTempDirectory();
    private readonly IBackgroundJobClient _jobs = Substitute.For<IBackgroundJobClient>();
    private readonly List<Job> _created = [];
    private readonly EmailTemplatesSettings _templates;

    private readonly ApplicationUser _user = new() { Email = "jane+test@elearning.test", FirstName = "Jane", LastName = "Doe" };

    public AppEmailServiceTests()
    {
        _jobs.Create(Arg.Do<Job>(_created.Add), Arg.Any<IState>()).Returns("job");
        _templates = new EmailTemplatesSettings
        {
            WelcomePath = Template("welcome.html", "Welcome {firstName}!"),
            ConfirmEmailPath = Template("confirm.html", "Hi {firstName}, <a href=\"{confirmationLink}\">confirm</a> token={token}"),
            ChangePasswordPath = Template("changed.html", "Dear {UserName}, your password changed."),
            ResetPasswordPath = Template("reset.html", "<a href=\"{resetLink}\">reset</a> token={token}"),
            TwoFactorCodePath = Template("2fa.html", "Hello {name}, code: {code}"),
        };
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Template(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private AppEmailService CreateService(FrontendSettings? frontend = null)
        => new(_jobs, Options.Create(_templates), Options.Create(frontend ?? TestSettings.Frontend()));

    private Job SingleJob() => Assert.Single(_created);
    private string Body() => (string)SingleJob().Args[2]!;
    private string Subject() => (string)SingleJob().Args[1]!;

    [Fact]
    public async Task SendWelcomeEmail_FillsFirstName()
    {
        await CreateService().SendWelcomeEmailAsync(_user);

        Assert.Equal("Welcome Jane!", Body());
        Assert.Equal("Welcome To E-Learning Platform", Subject());
    }

    [Fact]
    public async Task SendConfirmEmail_ContainsLinkAndToken()
    {
        await CreateService().SendConfirmEmailAsync(_user, "tok/en+=");

        var body = Body();
        Assert.Contains("Hi Jane", body);
        Assert.Contains("https://app.test/confirm-email?email=jane%2Btest%40elearning.test&amp;token=tok%2Fen%2B%3D", body);
        Assert.Contains("token=tok/en+=", body);
        Assert.Equal("Confirm Email", Subject());
    }

    [Fact]
    public async Task SendResetPasswordEmail_ContainsResetLink()
    {
        await CreateService().SendResetPasswordEmailAsync(_user, "abc");

        Assert.Contains("https://app.test/reset-password?email=jane%2Btest%40elearning.test&amp;token=abc", Body());
        Assert.Equal("Reset Password", Subject());
    }

    [Fact]
    public async Task SendTwoFactorEmail_ContainsFullNameAndCode()
    {
        await CreateService().SendTwoFactorEmailAsync(_user, "123456");

        Assert.Equal("Hello Jane Doe, code: 123456", Body());
        Assert.Equal("Two-Factor Authentication Code", Subject());
    }

    [Fact]
    public async Task SendPasswordChangedEmail_ContainsFullName()
    {
        await CreateService().SendPasswordChangedEmailAsync(_user);

        Assert.Equal("Dear Jane Doe, your password changed.", Body());
        Assert.Equal("Password Changed Successfully", Subject());
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    [InlineData("Tom & Jerry", "Tom &amp; Jerry")]
    [InlineData("\"quoted\"", "&quot;quoted&quot;")]
    public async Task UserNamesAreHtmlEncoded(string firstName, string encoded)
    {
        _user.FirstName = firstName;

        await CreateService().SendWelcomeEmailAsync(_user);

        Assert.Equal($"Welcome {encoded}!", Body());
        Assert.DoesNotContain("<script>", Body());
    }

    [Fact]
    public async Task TwoFactorEmail_EncodesLastNameToo()
    {
        _user.LastName = "<b>x</b>";

        await CreateService().SendTwoFactorEmailAsync(_user, "1");

        Assert.Contains("Jane &lt;b&gt;x&lt;/b&gt;", Body());
    }

    [Fact]
    public async Task EmailsAreSentToUserAsMailServiceJob()
    {
        await CreateService().SendWelcomeEmailAsync(_user);

        var job = SingleJob();
        Assert.Equal(typeof(IMailService), job.Type);
        Assert.Equal(nameof(IMailService.SendEmailAsync), job.Method.Name);
        Assert.Equal("jane+test@elearning.test", job.Args[0]);
        Assert.Null(job.Args[3]);
        _jobs.Received(1).Create(Arg.Any<Job>(), Arg.Any<EnqueuedState>());
    }

    [Fact]
    public async Task EveryEmailKindEnqueuesExactlyOneJob()
    {
        var service = CreateService();
        await service.SendWelcomeEmailAsync(_user);
        await service.SendConfirmEmailAsync(_user, "t");
        await service.SendResetPasswordEmailAsync(_user, "t");
        await service.SendTwoFactorEmailAsync(_user, "1");
        await service.SendPasswordChangedEmailAsync(_user);

        Assert.Equal(5, _created.Count);
    }

    [Fact]
    public async Task MissingTemplate_Throws()
    {
        _templates.WelcomePath = Path.Combine(_dir, "missing.html");

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateService().SendWelcomeEmailAsync(_user));
        Assert.Empty(_created);
    }

    [Theory]
    [InlineData("https://app.test", "/confirm-email", "https://app.test/confirm-email?")]
    [InlineData("https://app.test/", "/confirm-email", "https://app.test/confirm-email?")]
    [InlineData("https://app.test/", "confirm-email", "https://app.test/confirm-email?")]
    [InlineData("https://app.test", "confirm-email", "https://app.test/confirm-email?")]
    [InlineData("https://app.test/base/", "/x/y", "https://app.test/base/x/y?")]
    public void BuildLink_NormalizesSlashes(string baseUrl, string path, string expectedPrefix)
    {
        var link = CreateService(TestSettings.Frontend(baseUrl)).BuildLink(path, "a@b.c", "t");
        Assert.StartsWith(expectedPrefix, link);
    }

    [Theory]
    [InlineData("a@b.c", "x y", "email=a%40b.c&token=x%20y")]
    [InlineData("a+b@c.d", "p/q=r&s", "email=a%2Bb%40c.d&token=p%2Fq%3Dr%26s")]
    public void BuildLink_EscapesQueryValues(string email, string token, string expectedQuery)
    {
        var link = CreateService().BuildLink("/p", email, token);
        Assert.EndsWith("?" + expectedQuery, link);
    }

    [Fact]
    public async Task RelativeTemplatePath_IsResolvedAgainstAppBaseDirectory()
    {
        var relative = Path.GetRelativePath(AppContext.BaseDirectory, Template("rel.html", "R {firstName}"));
        if (Path.IsPathRooted(relative))
            return; // temp dir on another drive: nothing to check
        _templates.WelcomePath = relative;

        await CreateService().SendWelcomeEmailAsync(_user);

        Assert.Equal("R Jane", Body());
    }
}
