using Infra = ELearningPlatform.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Identity;

namespace ELearningPlatform.Application.Tests.Services;

public sealed class AuthServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IAuthService Sut => _ctx.Get<IAuthService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private static LoginDto Login(ApplicationUser user, string password = ServiceTestContext.Password, Role role = Role.Student)
        => new() { Email = user.Email!, Password = password, Role = role };

    // ---- Login ----

    [Fact]
    public async Task Login_with_valid_credentials_returns_tokens()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.LoginAsync(Login(user));

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Data!.UserId);
        Assert.Equal(RolesConstants.Student, result.Data.Role);
        Assert.False(string.IsNullOrEmpty(result.Data.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Data.RefreshToken));
    }

    [Fact]
    public async Task Login_stores_only_the_hash_of_the_refresh_token()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.LoginAsync(Login(user));

        var stored = await _ctx.Db.RefreshTokens.SingleAsync(t => t.ApplicationUserId == user.Id);
        Assert.NotEqual(result.Data!.RefreshToken, stored.TokenHash);
        Assert.Equal(_ctx.Get<IJwtTokenService>().HashRefreshToken(result.Data.RefreshToken), stored.TokenHash);
    }

    [Fact]
    public async Task Each_login_issues_a_new_refresh_token()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var first = await Sut.LoginAsync(Login(user));
        var second = await Sut.LoginAsync(Login(user));

        Assert.NotEqual(first.Data!.RefreshToken, second.Data!.RefreshToken);
        Assert.Equal(2, await _ctx.Db.RefreshTokens.CountAsync(t => t.ApplicationUserId == user.Id));
    }

    [Fact]
    public async Task Login_with_unknown_email_is_unauthorized()
    {
        var result = await Sut.LoginAsync(new LoginDto { Email = "nobody@test.com", Password = "x", Role = Role.Student });

        Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
        Assert.Equal(ErrorCodes.InvalidCredentials, result.Error.Code);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized_and_counts_a_failure()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.LoginAsync(Login(user, "Wrong-pass1!"));

        Assert.Equal(ErrorCodes.InvalidCredentials, result.Error.Code);
        Assert.Equal(1, await _ctx.Users.GetAccessFailedCountAsync(user));
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_give_the_same_error()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var unknown = await Sut.LoginAsync(new LoginDto { Email = "x@test.com", Password = "Wrong-pass1!", Role = Role.Student });
        var wrong = await Sut.LoginAsync(Login(user, "Wrong-pass1!"));

        Assert.Equal(unknown.Error, wrong.Error);
    }

    [Fact]
    public async Task Account_locks_after_max_failed_attempts()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        Result<AuthDto> last = null!;
        for (var i = 0; i < Infra.MaxFailedAccessAttempts; i++)
            last = await Sut.LoginAsync(Login(user, "Wrong-pass1!"));

        Assert.Equal(ErrorCodes.LockedOut, last.Error.Code);
        Assert.Equal(ErrorType.Forbidden, last.ErrorType);
        Assert.True(await _ctx.Users.IsLockedOutAsync(user));
    }

    [Fact]
    public async Task Locked_account_rejects_even_the_correct_password()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        for (var i = 0; i < Infra.MaxFailedAccessAttempts; i++)
            await Sut.LoginAsync(Login(user, "Wrong-pass1!"));

        var result = await Sut.LoginAsync(Login(user));

        Assert.Equal(ErrorCodes.LockedOut, result.Error.Code);
    }

    [Fact]
    public async Task Lockout_is_temporary_and_lasts_the_lockout_duration()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var before = DateTimeOffset.UtcNow; // Identity's lockout uses the system clock
        for (var i = 0; i < Infra.MaxFailedAccessAttempts; i++)
            await Sut.LoginAsync(Login(user, "Wrong-pass1!"));

        var lockoutEnd = await _ctx.Users.GetLockoutEndDateAsync(user);

        Assert.NotNull(lockoutEnd);
        Assert.InRange(lockoutEnd.Value, before + Infra.LockoutDuration, DateTimeOffset.UtcNow + Infra.LockoutDuration);
    }

    [Fact]
    public async Task Login_succeeds_again_once_the_lockout_has_ended()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        for (var i = 0; i < Infra.MaxFailedAccessAttempts; i++)
            await Sut.LoginAsync(Login(user, "Wrong-pass1!"));
        await _ctx.Users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1));

        var result = await Sut.LoginAsync(Login(user));

        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task Successful_login_resets_the_failure_counter()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await Sut.LoginAsync(Login(user, "Wrong-pass1!"));
        await Sut.LoginAsync(Login(user, "Wrong-pass1!"));

        await Sut.LoginAsync(Login(user));

        Assert.Equal(0, await _ctx.Users.GetAccessFailedCountAsync(user));
    }

    [Fact]
    public async Task Login_with_unconfirmed_email_is_forbidden()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, emailConfirmed: false);

        var result = await Sut.LoginAsync(Login(user));

        Assert.Equal(ErrorCodes.EmailNotConfirmed, result.Error.Code);
    }

    [Theory]
    [InlineData(Role.Teacher)]
    [InlineData(Role.Admin)]
    public async Task Login_with_a_role_the_user_lacks_is_forbidden(Role role)
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.LoginAsync(Login(user, role: role));

        Assert.Equal(ErrorCodes.InvalidRole, result.Error.Code);
    }

    [Fact]
    public async Task Login_with_two_factor_enabled_sends_code_instead_of_tokens()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await _ctx.Users.SetTwoFactorEnabledAsync(user, true);

        var result = await Sut.LoginAsync(Login(user));

        Assert.Equal(ErrorCodes.TwoFactorRequired, result.Error.Code);
        await _ctx.Email.Received(1).SendTwoFactorEmailAsync(Arg.Is<ApplicationUser>(u => u.Id == user.Id), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.False(await _ctx.Db.RefreshTokens.AnyAsync());
    }

    // ---- Refresh ----

    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var login = await Sut.LoginAsync(Login(user));

        var refreshed = await Sut.RefreshTokenAsync(login.Data!.RefreshToken);

        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(login.Data.RefreshToken, refreshed.Data!.RefreshToken);
        var old = await _ctx.Db.RefreshTokens.SingleAsync(t => t.TokenHash == _ctx.Get<IJwtTokenService>().HashRefreshToken(login.Data.RefreshToken));
        Assert.Equal(_ctx.Now, old.RevokedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Refresh_without_token_is_unauthorized(string? token)
    {
        var result = await Sut.RefreshTokenAsync(token!);

        Assert.Equal(ErrorCodes.RefreshTokenMissing, result.Error.Code);
    }

    [Fact]
    public async Task Refresh_with_unknown_token_is_unauthorized()
    {
        var result = await Sut.RefreshTokenAsync("not-a-real-token");

        Assert.Equal(ErrorCodes.InvalidRefreshToken, result.Error.Code);
    }

    [Fact]
    public async Task Refresh_with_expired_token_is_unauthorized()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var login = await Sut.LoginAsync(Login(user));

        _ctx.Time.Advance(TimeSpan.FromDays(8));
        var result = await Sut.RefreshTokenAsync(login.Data!.RefreshToken);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, result.Error.Code);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_every_session_of_the_user()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var login = await Sut.LoginAsync(Login(user));
        var otherDevice = await Sut.LoginAsync(Login(user));
        var rotated = await Sut.RefreshTokenAsync(login.Data!.RefreshToken);

        var reuse = await Sut.RefreshTokenAsync(login.Data.RefreshToken);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, reuse.Error.Code);
        Assert.False((await Sut.RefreshTokenAsync(rotated.Data!.RefreshToken)).IsSuccess);
        Assert.False((await Sut.RefreshTokenAsync(otherDevice.Data!.RefreshToken)).IsSuccess);
    }

    [Fact]
    public async Task Refresh_for_a_locked_user_is_forbidden()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var login = await Sut.LoginAsync(Login(user));
        await _ctx.Users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        var result = await Sut.RefreshTokenAsync(login.Data!.RefreshToken);

        Assert.Equal(ErrorCodes.LockedOut, result.Error.Code);
    }

    // ---- Revoke / logout ----

    [Fact]
    public async Task Revoke_marks_the_token_revoked()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var login = await Sut.LoginAsync(Login(user));

        var result = await Sut.RevokeTokenAsync(user.Id, login.Data!.RefreshToken);

        Assert.True(result.IsSuccess);
        Assert.False((await Sut.RefreshTokenAsync(login.Data.RefreshToken)).IsSuccess);
    }

    [Fact]
    public async Task Revoke_of_another_users_token_fails()
    {
        var owner = await _ctx.CreateUserAsync(RolesConstants.Student);
        var attacker = await _ctx.CreateUserAsync(RolesConstants.Student);
        var login = await Sut.LoginAsync(Login(owner));

        var result = await Sut.RevokeTokenAsync(attacker.Id, login.Data!.RefreshToken);

        Assert.Equal(ErrorCodes.InvalidRefreshToken, result.Error.Code);
        Assert.True((await Sut.RefreshTokenAsync(login.Data.RefreshToken)).IsSuccess);
    }

    [Fact]
    public async Task Revoke_twice_fails_the_second_time()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var login = await Sut.LoginAsync(Login(user));
        await Sut.RevokeTokenAsync(user.Id, login.Data!.RefreshToken);

        var result = await Sut.RevokeTokenAsync(user.Id, login.Data.RefreshToken);

        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Revoke_without_token_is_a_validation_error()
    {
        var result = await Sut.RevokeTokenAsync("user", "");

        Assert.Equal(ErrorCodes.RefreshTokenMissing, result.Error.Code);
    }

    [Fact]
    public async Task Logout_deletes_all_refresh_tokens_of_the_user_only()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        var other = await _ctx.CreateUserAsync(RolesConstants.Student);
        await Sut.LoginAsync(Login(user));
        await Sut.LoginAsync(Login(user));
        await Sut.LoginAsync(Login(other));

        var result = await Sut.LogoutAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await _ctx.Db.RefreshTokens.AnyAsync(t => t.ApplicationUserId == user.Id));
        Assert.True(await _ctx.Db.RefreshTokens.AnyAsync(t => t.ApplicationUserId == other.Id));
    }

    // ---- Two factor ----

    [Fact]
    public async Task Enable_two_factor_turns_it_on()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.EnableTwoFactorAuthenticationAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.True(await _ctx.Users.GetTwoFactorEnabledAsync(user));
    }

    [Fact]
    public async Task Enable_two_factor_twice_conflicts()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await Sut.EnableTwoFactorAuthenticationAsync(user.Id);

        var result = await Sut.EnableTwoFactorAuthenticationAsync(user.Id);

        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Disable_two_factor_when_off_conflicts()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.DisableTwoFactorAuthenticationAsync(user.Id);

        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Disable_two_factor_turns_it_off()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await Sut.EnableTwoFactorAuthenticationAsync(user.Id);

        var result = await Sut.DisableTwoFactorAuthenticationAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await _ctx.Users.GetTwoFactorEnabledAsync(user));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Toggle_two_factor_for_unknown_user_is_not_found(bool enable)
    {
        var result = enable
            ? await Sut.EnableTwoFactorAuthenticationAsync("missing")
            : await Sut.DisableTwoFactorAuthenticationAsync("missing");

        Assert.Equal(ErrorCodes.UserNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Verify_two_factor_with_valid_code_issues_tokens()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await _ctx.Users.SetTwoFactorEnabledAsync(user, true);
        var code = await _ctx.Users.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);

        var result = await Sut.VerifyTwoFactorAuthenticationAsync(new VerifyTwoFactorDto { Email = user.Email!, Code = code, Method = TwoFactorMethod.Email });

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Data!.AccessToken));
    }

    [Fact]
    public async Task Verify_two_factor_with_wrong_code_fails_and_counts_a_failure()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await _ctx.Users.SetTwoFactorEnabledAsync(user, true);

        var result = await Sut.VerifyTwoFactorAuthenticationAsync(new VerifyTwoFactorDto { Email = user.Email!, Code = "000000", Method = TwoFactorMethod.Email });

        Assert.Equal(ErrorCodes.InvalidTwoFactorCode, result.Error.Code);
        Assert.Equal(1, await _ctx.Users.GetAccessFailedCountAsync(user));
    }

    [Fact]
    public async Task Two_factor_codes_cannot_be_brute_forced()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await _ctx.Users.SetTwoFactorEnabledAsync(user, true);
        for (var i = 0; i < Infra.MaxFailedAccessAttempts; i++)
            await Sut.VerifyTwoFactorAuthenticationAsync(new VerifyTwoFactorDto { Email = user.Email!, Code = $"00000{i}", Method = TwoFactorMethod.Email });
        var code = await _ctx.Users.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);

        var result = await Sut.VerifyTwoFactorAuthenticationAsync(new VerifyTwoFactorDto { Email = user.Email!, Code = code, Method = TwoFactorMethod.Email });

        Assert.Equal(ErrorCodes.LockedOut, result.Error.Code);
    }

    [Fact]
    public async Task Verify_two_factor_for_unknown_email_looks_like_a_wrong_code()
    {
        var result = await Sut.VerifyTwoFactorAuthenticationAsync(new VerifyTwoFactorDto { Email = "x@test.com", Code = "123456", Method = TwoFactorMethod.Email });

        Assert.Equal(ErrorCodes.InvalidTwoFactorCode, result.Error.Code);
    }

    [Fact]
    public async Task Send_code_by_email_uses_the_email_service()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await _ctx.Users.SetTwoFactorEnabledAsync(user, true);

        var result = await Sut.SendTwoFactorCodeAsync(new TwoFactorSendCodeDto { Email = user.Email!, Method = TwoFactorMethod.Email });

        Assert.True(result.IsSuccess);
        await _ctx.Email.Received(1).SendTwoFactorEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _ctx.Notifications.DidNotReceive().SendSMS(Arg.Any<SendSMSDto>());
    }

    [Fact]
    public async Task Send_code_by_sms_goes_to_the_users_phone()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, phone: "+201011112222");
        await _ctx.Users.SetTwoFactorEnabledAsync(user, true);

        var result = await Sut.SendTwoFactorCodeAsync(new TwoFactorSendCodeDto { Email = user.Email!, Method = TwoFactorMethod.SMS });

        Assert.True(result.IsSuccess);
        _ctx.Notifications.Received(1).SendSMS(Arg.Is<SendSMSDto>(s => s.ToPhoneNumber == "+201011112222" && s.Body.Contains("verification code")));
    }

    [Fact]
    public async Task Send_code_by_sms_without_phone_is_a_validation_error()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, phone: null);
        await _ctx.Users.SetTwoFactorEnabledAsync(user, true);

        var result = await Sut.SendTwoFactorCodeAsync(new TwoFactorSendCodeDto { Email = user.Email!, Method = TwoFactorMethod.SMS });

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        _ctx.Notifications.DidNotReceive().SendSMS(Arg.Any<SendSMSDto>());
    }

    [Fact]
    public async Task Send_code_when_two_factor_disabled_is_a_validation_error()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.SendTwoFactorCodeAsync(new TwoFactorSendCodeDto { Email = user.Email!, Method = TwoFactorMethod.Email });

        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Send_code_for_unknown_email_is_not_found()
    {
        var result = await Sut.SendTwoFactorCodeAsync(new TwoFactorSendCodeDto { Email = "x@test.com", Method = TwoFactorMethod.Email });

        Assert.Equal(ErrorCodes.UserNotFound, result.Error.Code);
    }
}
