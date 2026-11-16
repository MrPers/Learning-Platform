using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.Abstractions.Services;
using ELearningPlatform.Application.DTOs.Authentication;

namespace ELearningPlatform.API.Tests.Account;

public class GoogleLoginTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private string ArrangeToken(GoogleUser? user)
    {
        var token = $"google-{Guid.NewGuid():N}";
        factory.Google.ValidateAsync(token, Arg.Any<CancellationToken>()).Returns(user);
        return token;
    }

    [Fact]
    public async Task GoogleLogin_ValidToken_CreatesStudentAndSignsIn()
    {
        var email = TestData.UniqueEmail("google");
        var token = ArrangeToken(new GoogleUser($"sub-{Guid.NewGuid():N}", email, true, "Gina", "Google"));

        var response = await factory.CreateHttpsClient().PostJsonAsync("api/Account/google-login", new { idToken = token, provider = "Google" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.ReadAsync<AuthDto>();
        Assert.Equal("Student", auth.Role);
        Assert.NotNull(response.RefreshSetCookie());
    }

    [Fact]
    public async Task GoogleLogin_SameGoogleAccountTwice_ReturnsSameUser()
    {
        var email = TestData.UniqueEmail("google");
        var subject = $"sub-{Guid.NewGuid():N}";
        var client = factory.CreateHttpsClient();

        var first = await (await client.PostJsonAsync("api/Account/google-login",
            new { idToken = ArrangeToken(new GoogleUser(subject, email, true, null, null)), provider = "Google" })).ReadAsync<AuthDto>();
        var second = await (await client.PostJsonAsync("api/Account/google-login",
            new { idToken = ArrangeToken(new GoogleUser(subject, email, true, null, null)), provider = "Google" })).ReadAsync<AuthDto>();

        Assert.Equal(first.UserId, second.UserId);
    }

    [Fact]
    public async Task GoogleLogin_ExistingLocalAccount_IsLinkedNotDuplicated()
    {
        var user = await factory.CreateUserAsync("Student");
        var token = ArrangeToken(new GoogleUser($"sub-{Guid.NewGuid():N}", user.Email, true, null, null));

        var response = await factory.CreateHttpsClient().PostJsonAsync("api/Account/google-login", new { idToken = token, provider = "Google" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(user.UserId, (await response.ReadAsync<AuthDto>()).UserId);
    }

    [Fact]
    public async Task GoogleLogin_UnverifiedEmail_Returns401()
    {
        var token = ArrangeToken(new GoogleUser($"sub-{Guid.NewGuid():N}", TestData.UniqueEmail(), false, null, null));

        var response = await factory.CreateHttpsClient().PostJsonAsync("api/Account/google-login", new { idToken = token, provider = "Google" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.InvalidExternalToken", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task GoogleLogin_InvalidToken_Returns401()
    {
        var token = ArrangeToken(null);

        var response = await factory.CreateHttpsClient().PostJsonAsync("api/Account/google-login", new { idToken = token, provider = "Google" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.InvalidExternalToken", await response.ReadProblemCodeAsync());
    }

    [Theory]
    [InlineData("", "Google")]
    [InlineData("token", "")]
    public async Task GoogleLogin_MissingFields_Returns400(string idToken, string provider)
    {
        var response = await factory.CreateHttpsClient().PostJsonAsync("api/Account/google-login", new { idToken, provider });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
