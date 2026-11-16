namespace ELearningPlatform.Application.Tests.Services;

public sealed class PasswordServiceTests : IAsyncLifetime
{
    private const string NewPassword = "N3w-Password!";
    private ServiceTestContext _ctx = null!;
    private IPasswordService Sut => _ctx.Get<IPasswordService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private async Task<ApplicationUser> UserWithSessionAsync()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await _ctx.Get<IAuthService>().LoginAsync(new LoginDto { Email = user.Email!, Password = ServiceTestContext.Password, Role = Role.Student });
        return user;
    }

    [Fact]
    public async Task Change_password_updates_it_and_signs_out_everywhere()
    {
        var user = await UserWithSessionAsync();

        var result = await Sut.ChangePasswordAsync(user.Id, new ChangePasswordDto { CurrentPassword = ServiceTestContext.Password, NewPassword = NewPassword });

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(await _ctx.Users.CheckPasswordAsync(user, NewPassword));
        Assert.False(await _ctx.Db.RefreshTokens.AnyAsync(t => t.ApplicationUserId == user.Id));
        await _ctx.Email.Received(1).SendPasswordChangedEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_fails()
    {
        var user = await UserWithSessionAsync();

        var result = await Sut.ChangePasswordAsync(user.Id, new ChangePasswordDto { CurrentPassword = "Wrong-1!", NewPassword = NewPassword });

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.True(await _ctx.Db.RefreshTokens.AnyAsync(t => t.ApplicationUserId == user.Id));
        await _ctx.Email.DidNotReceiveWithAnyArgs().SendPasswordChangedEmailAsync(default!, default);
    }

    [Fact]
    public async Task Change_password_to_a_weak_one_fails()
    {
        var user = await UserWithSessionAsync();

        var result = await Sut.ChangePasswordAsync(user.Id, new ChangePasswordDto { CurrentPassword = ServiceTestContext.Password, NewPassword = "weak" });

        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Change_password_for_unknown_user_is_not_found()
    {
        var result = await Sut.ChangePasswordAsync("missing", new ChangePasswordDto { CurrentPassword = "a", NewPassword = NewPassword });

        Assert.Equal(ErrorCodes.UserNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Forgot_password_sends_reset_email_for_known_user()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.ForgotPasswordAsync(new ForgotPasswordDto { Email = user.Email! });

        Assert.True(result.IsSuccess);
        await _ctx.Email.Received(1).SendResetPasswordEmailAsync(Arg.Is<ApplicationUser>(u => u.Id == user.Id), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Forgot_password_gives_the_same_answer_for_unknown_email()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var known = await Sut.ForgotPasswordAsync(new ForgotPasswordDto { Email = user.Email! });
        var unknown = await Sut.ForgotPasswordAsync(new ForgotPasswordDto { Email = "nobody@test.com" });

        Assert.Equal(known.IsSuccess, unknown.IsSuccess);
        Assert.Equal(known.Message, unknown.Message);
        await _ctx.Email.Received(1).SendResetPasswordEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_password_with_valid_token_changes_password_and_ends_sessions()
    {
        var user = await UserWithSessionAsync();
        var token = await _ctx.Users.GeneratePasswordResetTokenAsync(user);

        var result = await Sut.ResetPasswordAsync(new ResetPasswordDto { Email = user.Email!, Token = token, NewPassword = NewPassword });

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(await _ctx.Users.CheckPasswordAsync(user, NewPassword));
        Assert.False(await _ctx.Db.RefreshTokens.AnyAsync(t => t.ApplicationUserId == user.Id));
    }

    [Fact]
    public async Task Reset_password_with_invalid_token_fails()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.ResetPasswordAsync(new ResetPasswordDto { Email = user.Email!, Token = "bad", NewPassword = NewPassword });

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.True(await _ctx.Users.CheckPasswordAsync(user, ServiceTestContext.Password));
    }

    [Fact]
    public async Task Reset_password_for_unknown_email_does_not_reveal_it()
    {
        var result = await Sut.ResetPasswordAsync(new ResetPasswordDto { Email = "x@test.com", Token = "t", NewPassword = NewPassword });

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.NotEqual(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Reset_token_can_be_used_only_once()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var token = await _ctx.Users.GeneratePasswordResetTokenAsync(user);
        await Sut.ResetPasswordAsync(new ResetPasswordDto { Email = user.Email!, Token = token, NewPassword = NewPassword });

        var second = await Sut.ResetPasswordAsync(new ResetPasswordDto { Email = user.Email!, Token = token, NewPassword = "An0ther-Pass!" });

        Assert.False(second.IsSuccess);
    }
}
