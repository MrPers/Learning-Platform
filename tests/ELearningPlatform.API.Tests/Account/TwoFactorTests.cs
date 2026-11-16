using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.DTOs.Authentication;

namespace ELearningPlatform.API.Tests.Account;

public class TwoFactorTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<TestUser> CreateUserWithTwoFactorAsync()
    {
        var user = await factory.CreateUserAsync("Student");
        var enable = await user.Client.PostAsync("api/Account/2fa-enable", null);
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        return user;
    }

    [Fact]
    public async Task Enable_Anonymous_Returns401()
    {
        var response = await factory.CreateHttpsClient().PostAsync("api/Account/2fa-enable", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enable_Twice_Returns409()
    {
        var user = await CreateUserWithTwoFactorAsync();

        var response = await user.Client.PostAsync("api/Account/2fa-enable", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Disable_WhenNotEnabled_Returns409()
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await user.Client.PostAsync("api/Account/2fa-disable", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithTwoFactor_RequiresCode_ThenVerifyIssuesTokens()
    {
        var user = await CreateUserWithTwoFactorAsync();
        var client = factory.CreateHttpsClient();

        var login = await client.LoginAsync(user.Email, user.Password, "Student");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("Auth.TwoFactorRequired", await login.ReadProblemCodeAsync());
        Assert.Null(login.RefreshSetCookie());

        var code = factory.Emails.TwoFactorCodes[user.Email];
        var verify = await client.PostJsonAsync("api/Account/2fa-verify", new { email = user.Email, method = "Email", code });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.False(string.IsNullOrEmpty((await verify.ReadAsync<AuthDto>()).AccessToken));
        Assert.NotNull(verify.RefreshSetCookie());
    }

    [Fact]
    public async Task Verify_WrongCode_Returns401()
    {
        var user = await CreateUserWithTwoFactorAsync();
        await factory.CreateHttpsClient().LoginAsync(user.Email, user.Password, "Student");
        var wrong = factory.Emails.TwoFactorCodes[user.Email] == "000000" ? "111111" : "000000";

        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/2fa-verify", new { email = user.Email, method = "Email", code = wrong });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.InvalidTwoFactorCode", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Verify_UnknownEmail_Returns401SameCode()
    {
        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/2fa-verify", new { email = TestData.UniqueEmail(), method = "Email", code = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.InvalidTwoFactorCode", await response.ReadProblemCodeAsync());
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdef")]
    [InlineData("")]
    public async Task Verify_MalformedCode_Returns400(string code)
    {
        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/2fa-verify", new { email = TestData.UniqueEmail(), method = "Email", code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Verify_RepeatedWrongCodes_LockTheAccount()
    {
        var user = await CreateUserWithTwoFactorAsync();
        var client = factory.CreateHttpsClient();
        await client.LoginAsync(user.Email, user.Password, "Student");
        var wrong = factory.Emails.TwoFactorCodes[user.Email] == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
            await client.PostJsonAsync("api/Account/2fa-verify", new { email = user.Email, method = "Email", code = wrong });

        var login = await client.LoginAsync(user.Email, user.Password, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("Auth.LockedOut", await login.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task SendCode_ByEmail_RecordsNewCode()
    {
        var user = await CreateUserWithTwoFactorAsync();

        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/2fa/send-code", new { email = user.Email, method = "Email" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(factory.Emails.TwoFactorCodes.ContainsKey(user.Email));
    }

    [Fact]
    public async Task SendCode_WhenTwoFactorDisabled_Returns400()
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/2fa/send-code", new { email = user.Email, method = "Email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendCode_BySms_QueuesSms()
    {
        var user = await CreateUserWithTwoFactorAsync();

        var response = await factory.CreateHttpsClient()
            .PostJsonAsync("api/Account/2fa/send-code", new { email = user.Email, method = "SMS" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.Notifications.Received().SendSMS(Arg.Is<Application.DTOs.Notification.SendSMSDto>(d => d.ToPhoneNumber == TestData.Phone));
    }

    [Fact]
    public async Task Disable_ThenLogin_NoLongerRequiresCode()
    {
        var user = await CreateUserWithTwoFactorAsync();
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PostAsync("api/Account/2fa-disable", null)).StatusCode);

        var login = await factory.CreateHttpsClient().LoginAsync(user.Email, user.Password, "Student");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
