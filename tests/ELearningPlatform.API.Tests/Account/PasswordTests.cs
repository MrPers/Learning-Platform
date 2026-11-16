using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;

namespace ELearningPlatform.API.Tests.Account;

public class PasswordTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string NewPassword = "N3w!Password";

    [Fact]
    public async Task ChangePassword_Anonymous_Returns401()
    {
        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/change-password", new { currentPassword = TestData.Password, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_Valid_SwitchesPasswordAndEndsSessions()
    {
        var user = await factory.CreateUserAsync("Student");
        var login = await user.Client.LoginAsync(user.Email, user.Password, "Student");
        var cookie = login.RefreshCookieValue()!;

        var response = await user.Client.PostJsonAsync("api/Account/change-password",
            new { currentPassword = user.Password, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.RefreshWithCookieAsync(cookie)).StatusCode);
        var client = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(user.Email, user.Password, "Student")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(user.Email, NewPassword, "Student")).StatusCode);
        Assert.Equal(1, factory.Emails.PasswordChangedEmails[user.Email]);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400()
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await user.Client.PostJsonAsync("api/Account/change-password",
            new { currentPassword = "Wrong!Pass1", newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("weak")]
    [InlineData(TestData.Password)]
    public async Task ChangePassword_WeakOrUnchangedNewPassword_Returns400(string newPassword)
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await user.Client.PostJsonAsync("api/Account/change-password",
            new { currentPassword = user.Password, newPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("NewPassword", await response.ReadValidationErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_SameResponseForKnownAndUnknownEmail()
    {
        var user = await factory.CreateUserAsync("Student");
        var client = factory.CreateHttpsClient();

        var known = await client.PostJsonAsync("api/Account/forgot-password", new { email = user.Email });
        var unknown = await client.PostJsonAsync("api/Account/forgot-password", new { email = TestData.UniqueEmail() });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.True(factory.Emails.ResetTokens.ContainsKey(user.Email));
    }

    [Fact]
    public async Task ResetPassword_EndToEnd()
    {
        var user = await factory.CreateUserAsync("Student");
        var client = factory.CreateHttpsClient();
        await client.PostJsonAsync("api/Account/forgot-password", new { email = user.Email });
        var token = factory.Emails.ResetTokens[user.Email];

        var reset = await client.PostJsonAsync("api/Account/reset-password", new { email = user.Email, token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(user.Email, user.Password, "Student")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(user.Email, NewPassword, "Student")).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_TokenCanOnlyBeUsedOnce()
    {
        var user = await factory.CreateUserAsync("Student");
        var client = factory.CreateHttpsClient();
        await client.PostJsonAsync("api/Account/forgot-password", new { email = user.Email });
        var token = factory.Emails.ResetTokens[user.Email];
        await client.PostJsonAsync("api/Account/reset-password", new { email = user.Email, token, newPassword = NewPassword });

        var again = await client.PostJsonAsync("api/Account/reset-password", new { email = user.Email, token, newPassword = "An0ther!Pass" });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Theory]
    [InlineData("bogus-token", NewPassword)]
    [InlineData("", NewPassword)]
    [InlineData("bogus-token", "weak")]
    public async Task ResetPassword_InvalidInput_Returns400(string token, string newPassword)
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/reset-password", new { email = user.Email, token, newPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_UnknownEmail_Returns400NotRevealingAccount()
    {
        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/reset-password", new { email = TestData.UniqueEmail(), token = "x", newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
