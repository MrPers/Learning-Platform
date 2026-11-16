using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.DTOs.Authentication;

namespace ELearningPlatform.API.Tests.Account;

public class RegistrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    public async Task Register_ValidForm_ReturnsOkAndSendsConfirmationEmail(string role)
    {
        var email = TestData.UniqueEmail();
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsync($"api/Account/register-{role.ToLowerInvariant()}", TestData.RegisterForm(email, role));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(factory.Emails.ConfirmTokens.ContainsKey(email));
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409WithCode()
    {
        var email = TestData.UniqueEmail();
        var client = factory.CreateHttpsClient();
        await client.PostAsync("api/Account/register-student", TestData.RegisterForm(email, "Student"));

        var response = await client.PostAsync("api/Account/register-teacher", TestData.RegisterForm(email, "Teacher"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Auth.EmailAlreadyRegistered", await response.ReadProblemCodeAsync());
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSymbols123")]
    public async Task Register_WeakPassword_Returns400WithPasswordError(string password)
    {
        var response = await factory.CreateHttpsClient()
            .PostAsync("api/Account/register-student", TestData.RegisterForm(TestData.UniqueEmail(), "Student", password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Password", await response.ReadValidationErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_PasswordMismatch_Returns400WithConfirmPasswordError()
    {
        var response = await factory.CreateHttpsClient().PostAsync("api/Account/register-student",
            TestData.RegisterForm(TestData.UniqueEmail(), "Student", confirmPassword: "Other!Pass1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ConfirmPassword", await response.ReadValidationErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("+15551234567")]
    public async Task Register_InvalidPhone_Returns400(string phone)
    {
        var response = await factory.CreateHttpsClient().PostAsync("api/Account/register-student",
            TestData.RegisterForm(TestData.UniqueEmail(), "Student", phone: phone));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("PhoneNumber", await response.ReadValidationErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_RealEgyptianNumber_IsAccepted()
    {
        var response = await factory.CreateHttpsClient().PostAsync("api/Account/register-student",
            TestData.RegisterForm(TestData.UniqueEmail(), "Student", phone: "+201012345678"));

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task Register_ProfilePictureWithFakeContent_Returns400()
    {
        var response = await factory.CreateHttpsClient().PostAsync("api/Account/register-student",
            TestData.RegisterForm(TestData.UniqueEmail(), "Student", picture: (TestData.Exe, "me.png", "image/png")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ProfilePictureFile", await response.ReadValidationErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ValidProfilePicture_StoresFile()
    {
        var email = TestData.UniqueEmail();
        var response = await factory.CreateHttpsClient().PostAsync("api/Account/register-student",
            TestData.RegisterForm(email, "Student", picture: (TestData.Png, "me.png", "image/png")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(factory.WebRoot, "Uploads", "Images")));
    }

    [Fact]
    public async Task ConfirmEmail_ReturnsTokensAndSetsSecureHttpOnlyRefreshCookie()
    {
        var email = TestData.UniqueEmail();
        var client = factory.CreateHttpsClient();
        await client.PostAsync("api/Account/register-student", TestData.RegisterForm(email, "Student"));
        var token = factory.Emails.ConfirmTokens[email];

        var response = await client.GetAsync($"api/Account/confirm-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.ReadAsync<AuthDto>();
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.Equal("Student", auth.Role);

        var cookie = response.RefreshSetCookie();
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/Account", cookie, StringComparison.OrdinalIgnoreCase);

        var body = await response.ReadJsonAsync();
        Assert.False(body.TryGetProperty("refreshToken", out _), "the refresh token must never be in the JSON body");
        Assert.Equal(1, factory.Emails.WelcomeEmails[email]);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_Returns400()
    {
        var email = TestData.UniqueEmail();
        var client = factory.CreateHttpsClient();
        await client.PostAsync("api/Account/register-student", TestData.RegisterForm(email, "Student"));

        var response = await client.GetAsync($"api/Account/confirm-email?email={Uri.EscapeDataString(email)}&token=bogus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_UnknownUser_Returns404()
    {
        var response = await factory.CreateHttpsClient().GetAsync($"api/Account/confirm-email?email={TestData.UniqueEmail()}&token=x");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("User.NotFound", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task ConfirmEmail_MissingQuery_Returns400()
    {
        var response = await factory.CreateHttpsClient().GetAsync("api/Account/confirm-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmEmail_UnconfirmedUser_IssuesNewToken()
    {
        var email = TestData.UniqueEmail();
        var client = factory.CreateHttpsClient();
        await client.PostAsync("api/Account/register-student", TestData.RegisterForm(email, "Student"));
        factory.Emails.ConfirmTokens.TryRemove(email, out _);

        var response = await client.PostJsonAsync("api/Account/resend-confirm-email", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(factory.Emails.ConfirmTokens.ContainsKey(email));
    }

    [Fact]
    public async Task ResendConfirmEmail_AlreadyConfirmed_Returns409()
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await user.Client.PostJsonAsync("api/Account/resend-confirm-email", new { email = user.Email });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmEmail_UnknownEmail_LooksLikeSuccess()
    {
        var response = await factory.CreateHttpsClient().PostJsonAsync("api/Account/resend-confirm-email", new { email = TestData.UniqueEmail() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
