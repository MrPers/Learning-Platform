using System.Net;
using System.Net.Http.Headers;
using ELearningPlatform.Application.DTOs.Authentication;

namespace ELearningPlatform.API.Tests.Infrastructure;

public sealed record TestUser(string Email, string Password, string UserId, string AccessToken, HttpClient Client);

public static class AuthHelper
{
    public const string RefreshCookie = "refreshToken";

    /// <summary>Registers, confirms (which signs in) and returns a client carrying the bearer token and refresh cookie.</summary>
    public static async Task<TestUser> CreateUserAsync(this ApiFactory factory, string role,
        string? email = null, string password = TestData.Password, string specialization = "ComputerScience")
    {
        email ??= TestData.UniqueEmail(role.ToLowerInvariant());
        var client = factory.CreateHttpsClient();

        var register = await client.PostAsync($"api/Account/register-{role.ToLowerInvariant()}",
            TestData.RegisterForm(email, role, password, specialization: specialization));
        await ResponseAssert.StatusAsync(HttpStatusCode.OK, register);

        var auth = await client.ConfirmEmailAsync(factory, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(email, password, auth.UserId, auth.AccessToken, client);
    }

    public static async Task<AuthDto> ConfirmEmailAsync(this HttpClient client, ApiFactory factory, string email)
    {
        var token = factory.Emails.ConfirmTokens[email];
        var response = await client.GetAsync($"api/Account/confirm-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}");
        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
        return await response.ReadAsync<AuthDto>();
    }

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password, string role)
        => client.PostJsonAsync("api/Account/login", new { email, password, role });

    public static async Task<TestUser> LoginAdminAsync(this ApiFactory factory)
    {
        var client = factory.CreateHttpsClient();
        var response = await client.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword, "Admin");
        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
        var auth = await response.ReadAsync<AuthDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new TestUser(ApiFactory.AdminEmail, ApiFactory.AdminPassword, auth.UserId, auth.AccessToken, client);
    }

    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>The raw Set-Cookie header for the refresh token, if the response set one.</summary>
    public static string? RefreshSetCookie(this HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(RefreshCookie + "=", StringComparison.Ordinal))
            : null;

    public static string? RefreshCookieValue(this HttpResponseMessage response)
    {
        var header = response.RefreshSetCookie();
        if (header == null)
            return null;
        var pair = header.Split(';')[0];
        return pair[(RefreshCookie.Length + 1)..];
    }

    /// <summary>Calls refresh-token with an explicit cookie value on a client that doesn't manage cookies.</summary>
    public static Task<HttpResponseMessage> RefreshWithCookieAsync(this ApiFactory factory, string cookieValue)
    {
        var client = factory.CreateHttpsClient(handleCookies: false);
        var request = new HttpRequestMessage(HttpMethod.Post, "api/Account/refresh-token");
        request.Headers.Add("Cookie", $"{RefreshCookie}={cookieValue}");
        return client.SendAsync(request);
    }
}
