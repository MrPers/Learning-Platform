using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.DTOs.Authentication;

namespace ELearningPlatform.API.Tests.Account;

public class LoginTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_ConfirmedUser_ReturnsTokenAndCookie()
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await factory.CreateHttpsClient().LoginAsync(user.Email, user.Password, "Student");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.ReadAsync<AuthDto>();
        Assert.Equal(user.UserId, auth.UserId);
        Assert.True(auth.ExpiresOn > DateTime.UtcNow);
        Assert.True(auth.RefreshTokenExpiration > auth.ExpiresOn);
        Assert.NotNull(response.RefreshSetCookie());
    }

    [Fact]
    public async Task Login_SeededAdmin_Works()
    {
        var response = await factory.CreateHttpsClient().LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword, "Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Admin", (await response.ReadAsync<AuthDto>()).Role);
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_Returns403()
    {
        var email = TestData.UniqueEmail();
        var client = factory.CreateHttpsClient();
        await client.PostAsync("api/Account/register-student", TestData.RegisterForm(email, "Student"));

        var response = await client.LoginAsync(email, TestData.Password, "Student");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Auth.EmailNotConfirmed", await response.ReadProblemCodeAsync());
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Admin")]
    public async Task Login_WrongRole_Returns403(string role)
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await factory.CreateHttpsClient().LoginAsync(user.Email, user.Password, role);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Auth.InvalidRole", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await factory.CreateHttpsClient().LoginAsync(user.Email, "Wrong!Pass1", "Student");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.InvalidCredentials", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401WithSameCodeAsWrongPassword()
    {
        var response = await factory.CreateHttpsClient().LoginAsync(TestData.UniqueEmail(), "Wrong!Pass1", "Student");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.InvalidCredentials", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksAccount_EvenForCorrectPassword()
    {
        var user = await factory.CreateUserAsync("Student");
        var client = factory.CreateHttpsClient();

        for (var i = 1; i <= 4; i++)
        {
            var attempt = await client.LoginAsync(user.Email, "Wrong!Pass1", "Student");
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }
        var fifth = await client.LoginAsync(user.Email, "Wrong!Pass1", "Student");
        Assert.Equal(HttpStatusCode.Forbidden, fifth.StatusCode);
        Assert.Equal("Auth.LockedOut", await fifth.ReadProblemCodeAsync());

        var correct = await client.LoginAsync(user.Email, user.Password, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, correct.StatusCode);
        Assert.Equal("Auth.LockedOut", await correct.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsFailedAttemptCounter()
    {
        var user = await factory.CreateUserAsync("Student");
        var client = factory.CreateHttpsClient();

        for (var i = 0; i < 4; i++)
            await client.LoginAsync(user.Email, "Wrong!Pass1", "Student");
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(user.Email, user.Password, "Student")).StatusCode);

        for (var i = 0; i < 4; i++)
            await client.LoginAsync(user.Email, "Wrong!Pass1", "Student");
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(user.Email, user.Password, "Student")).StatusCode);
    }

    [Theory]
    [InlineData("", "Pass!1")]
    [InlineData("not-an-email", "Pass!1")]
    [InlineData("a@b.com", "")]
    public async Task Login_InvalidBody_Returns400(string email, string password)
    {
        var response = await factory.CreateHttpsClient().LoginAsync(email, password, "Student");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AccessToken_FromLogin_AuthorizesProtectedEndpoint()
    {
        var user = await factory.CreateUserAsync("Student");

        var response = await user.Client.GetAsync("api/Courses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
