namespace ELearningPlatform.Application.Tests.Services;

public sealed class AuthTokenIssuerTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private AuthTokenIssuer Sut => _ctx.Get<AuthTokenIssuer>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Issue_returns_access_and_refresh_tokens_with_expiries()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Teacher);

        var auth = await Sut.IssueAsync(user);

        Assert.Equal(user.Id, auth.UserId);
        Assert.Equal(RolesConstants.Teacher, auth.Role);
        Assert.Equal(_ctx.Now.AddMinutes(15), auth.ExpiresOn);
        Assert.Equal(_ctx.Now.AddDays(7), auth.RefreshTokenExpiration);
    }

    [Fact]
    public async Task Issue_persists_the_refresh_token()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Teacher);

        var auth = await Sut.IssueAsync(user);

        var stored = await _ctx.InNewScopeAsync(sp => sp.GetRequiredService<AppDbContext>().RefreshTokens.SingleAsync());
        Assert.Equal(user.Id, stored.ApplicationUserId);
        Assert.Equal(_ctx.Get<IJwtTokenService>().HashRefreshToken(auth.RefreshToken), stored.TokenHash);
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task User_without_roles_defaults_to_student()
    {
        var user = new ApplicationUser { UserName = "noroles", Email = "noroles@test.com", EmailConfirmed = true };
        await _ctx.Users.CreateAsync(user, ServiceTestContext.Password);

        var auth = await Sut.IssueAsync(user);

        Assert.Equal(RolesConstants.Student, auth.Role);
    }

    [Fact]
    public async Task Refresh_token_is_never_serialized_into_json()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var auth = await Sut.IssueAsync(user);

        var json = System.Text.Json.JsonSerializer.Serialize(auth);

        Assert.DoesNotContain(auth.RefreshToken, json);
        Assert.Contains(auth.AccessToken, json);
    }
}
