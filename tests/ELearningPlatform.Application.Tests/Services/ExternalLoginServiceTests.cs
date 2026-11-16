namespace ELearningPlatform.Application.Tests.Services;

public sealed class ExternalLoginServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IExternalLoginService Sut => _ctx.Get<IExternalLoginService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private static ExternalLoginDto Dto => new() { IdToken = "id-token", Provider = "Google" };

    private void GoogleReturns(GoogleUser? user)
        => _ctx.Google.ValidateAsync("id-token", Arg.Any<CancellationToken>()).Returns(user);

    [Fact]
    public async Task Invalid_google_token_is_unauthorized()
    {
        GoogleReturns(null);

        var result = await Sut.GoogleLoginAsync(Dto);

        Assert.Equal(ErrorCodes.InvalidExternalToken, result.Error.Code);
    }

    [Fact]
    public async Task Unverified_google_email_is_rejected()
    {
        GoogleReturns(new GoogleUser("sub-1", "a@gmail.com", EmailVerified: false, "A", "B"));

        var result = await Sut.GoogleLoginAsync(Dto);

        Assert.Equal(ErrorCodes.InvalidExternalToken, result.Error.Code);
        Assert.Null(await _ctx.Users.FindByEmailAsync("a@gmail.com"));
    }

    [Fact]
    public async Task New_google_user_becomes_a_confirmed_student_with_a_linked_login()
    {
        GoogleReturns(new GoogleUser("sub-2", "new@gmail.com", true, "Nina", "Ng"));

        var result = await Sut.GoogleLoginAsync(Dto);

        Assert.True(result.IsSuccess, result.Message);
        var user = await _ctx.Users.FindByEmailAsync("new@gmail.com");
        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
        Assert.Equal("Nina", user.FirstName);
        Assert.Equal("Ng", user.LastName);
        Assert.True(await _ctx.Users.IsInRoleAsync(user, RolesConstants.Student));
        Assert.Equal(user.Id, (await _ctx.Users.FindByLoginAsync(ExternalLoginService.GoogleProvider, "sub-2"))?.Id);
        Assert.Equal(RolesConstants.Student, result.Data!.Role);
    }

    [Fact]
    public async Task Existing_account_with_same_email_is_linked_not_duplicated()
    {
        var existing = await _ctx.CreateUserAsync(RolesConstants.Teacher, email: "t@gmail.com");
        GoogleReturns(new GoogleUser("sub-3", "t@gmail.com", true, null, null));

        var result = await Sut.GoogleLoginAsync(Dto);

        Assert.Equal(existing.Id, result.Data!.UserId);
        Assert.Equal(RolesConstants.Teacher, result.Data.Role);
        Assert.Equal(1, await _ctx.Db.Users.CountAsync(u => u.Email == "t@gmail.com"));
        Assert.Single(await _ctx.Users.GetLoginsAsync(existing));
    }

    [Fact]
    public async Task Second_google_login_finds_user_by_login_and_does_not_add_another_link()
    {
        GoogleReturns(new GoogleUser("sub-4", "again@gmail.com", true, "A", "B"));
        var first = await Sut.GoogleLoginAsync(Dto);

        var second = await Sut.GoogleLoginAsync(Dto);

        Assert.Equal(first.Data!.UserId, second.Data!.UserId);
        var user = await _ctx.Users.FindByIdAsync(first.Data.UserId);
        Assert.Single(await _ctx.Users.GetLoginsAsync(user!));
    }

    [Fact]
    public async Task Locked_account_cannot_sign_in_with_google()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, email: "locked@gmail.com");
        await _ctx.Users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        GoogleReturns(new GoogleUser("sub-5", "locked@gmail.com", true, null, null));

        var result = await Sut.GoogleLoginAsync(Dto);

        Assert.Equal(ErrorCodes.LockedOut, result.Error.Code);
    }

    [Fact]
    public async Task Google_login_issues_a_refresh_token()
    {
        GoogleReturns(new GoogleUser("sub-6", "rt@gmail.com", true, null, null));

        var result = await Sut.GoogleLoginAsync(Dto);

        Assert.False(string.IsNullOrEmpty(result.Data!.RefreshToken));
        Assert.True(await _ctx.Db.RefreshTokens.AnyAsync(t => t.ApplicationUserId == result.Data.UserId));
    }
}
