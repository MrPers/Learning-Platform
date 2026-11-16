using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.DTOs.Authentication;

namespace ELearningPlatform.API.Tests.Account;

public class RefreshTokenTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(TestUser User, string Cookie)> SignInAsync()
    {
        var user = await factory.CreateUserAsync("Student");
        var login = await user.Client.LoginAsync(user.Email, user.Password, "Student");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (user, login.RefreshCookieValue()!);
    }

    [Fact]
    public async Task Refresh_WithCookie_ReturnsNewAccessTokenAndRotatesCookie()
    {
        var (user, oldCookie) = await SignInAsync();

        var response = await user.Client.PostAsync("api/Account/refresh-token", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.ReadAsync<AuthDto>();
        Assert.Equal(user.UserId, auth.UserId);
        var newCookie = response.RefreshCookieValue();
        Assert.NotNull(newCookie);
        Assert.NotEqual(oldCookie, newCookie);
    }

    [Fact]
    public async Task Refresh_NewAccessToken_Works()
    {
        var (user, _) = await SignInAsync();
        var auth = await (await user.Client.PostAsync("api/Account/refresh-token", null)).ReadAsync<AuthDto>();

        var response = await factory.CreateHttpsClient().WithBearer(auth.AccessToken).GetAsync("api/Courses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedToken_Returns401AndRevokesAllSessions()
    {
        var (user, oldCookie) = await SignInAsync();
        var rotated = await user.Client.PostAsync("api/Account/refresh-token", null);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);

        var reuse = await factory.RefreshWithCookieAsync(oldCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("Auth.InvalidRefreshToken", await reuse.ReadProblemCodeAsync());

        // The legitimate (newest) token was revoked too, as the old one looked stolen
        var afterReuse = await user.Client.PostAsync("api/Account/refresh-token", null);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401Missing()
    {
        var response = await factory.CreateHttpsClient(handleCookies: false).PostAsync("api/Account/refresh-token", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.RefreshTokenMissing", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401Invalid()
    {
        var response = await factory.RefreshWithCookieAsync("this-token-was-never-issued");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Auth.InvalidRefreshToken", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Logout_ClearsCookieAndInvalidatesRefreshToken()
    {
        var (user, cookie) = await SignInAsync();

        var logout = await user.Client.PostAsync("api/Account/logout", null);

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        var cleared = logout.RefreshSetCookie();
        Assert.NotNull(cleared);
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.RefreshWithCookieAsync(cookie)).StatusCode);
    }

    [Fact]
    public async Task Logout_Anonymous_Returns401()
    {
        var response = await factory.CreateHttpsClient().PostAsync("api/Account/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RevokeToken_ByBody_RevokesThatTokenOnly()
    {
        var (user, firstCookie) = await SignInAsync();
        var second = await factory.CreateHttpsClient().LoginAsync(user.Email, user.Password, "Student");
        var secondCookie = second.RefreshCookieValue()!;

        var revoke = await user.Client.PostJsonAsync("api/Account/revoke-token", new { token = firstCookie });

        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        // Other session is untouched (checked first: presenting a revoked token later ends every session)
        Assert.Equal(HttpStatusCode.OK, (await factory.RefreshWithCookieAsync(secondCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.RefreshWithCookieAsync(firstCookie)).StatusCode);
    }

    [Fact]
    public async Task RevokeToken_TwiceSameToken_SecondIs400()
    {
        var (user, cookie) = await SignInAsync();
        await user.Client.PostJsonAsync("api/Account/revoke-token", new { token = cookie });

        var again = await user.Client.PostJsonAsync("api/Account/revoke-token", new { token = cookie });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("Auth.InvalidRefreshToken", await again.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task RevokeToken_OfAnotherUser_IsRejected()
    {
        var (_, victimCookie) = await SignInAsync();
        var attacker = await factory.CreateUserAsync("Student");

        var response = await attacker.Client.PostJsonAsync("api/Account/revoke-token", new { token = victimCookie });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.RefreshWithCookieAsync(victimCookie)).StatusCode);
    }
}
