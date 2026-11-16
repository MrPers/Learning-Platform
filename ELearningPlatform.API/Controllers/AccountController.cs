using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ELearningPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AccountController(
    IAuthService authService,
    IRegistrationService registrationService,
    IPasswordService passwordService,
    IExternalLoginService externalLoginService) : ControllerBase
{
    [HttpPost("register-teacher")]
    public async Task<IActionResult> RegisterTeacher([FromForm] RegisterTeacherDto dto, CancellationToken cancellationToken)
        => (await registrationService.RegisterTeacherAsync(dto, cancellationToken)).ToActionResult(this);

    [HttpPost("register-student")]
    public async Task<IActionResult> RegisterStudent([FromForm] RegisterStudentDto dto, CancellationToken cancellationToken)
        => (await registrationService.RegisterStudentAsync(dto, cancellationToken)).ToActionResult(this);

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] ConfirmEmailDto dto, CancellationToken cancellationToken)
        => WithRefreshCookie(await registrationService.ConfirmEmailAsync(dto.Email, dto.Token, cancellationToken));

    [HttpPost("resend-confirm-email")]
    public async Task<IActionResult> ResendConfirmEmail(ResendConfirmEmailDto dto, CancellationToken cancellationToken)
        => (await registrationService.ResendEmailConfirmationAsync(dto, cancellationToken)).ToActionResult(this);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
        => WithRefreshCookie(await authService.LoginAsync(dto, cancellationToken));

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin(ExternalLoginDto dto, CancellationToken cancellationToken)
        => WithRefreshCookie(await externalLoginService.GoogleLoginAsync(dto, cancellationToken));

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
        => WithRefreshCookie(await authService.RefreshTokenAsync(RefreshTokenCookie.Read(Request) ?? string.Empty, cancellationToken));

    [Authorize]
    [HttpPost("revoke-token")]
    public async Task<IActionResult> RevokeToken(RevokeTokenRequest? request, CancellationToken cancellationToken)
    {
        var token = request?.Token ?? RefreshTokenCookie.Read(Request) ?? string.Empty;
        var result = await authService.RevokeTokenAsync(User.GetUserId(), token, cancellationToken);
        if (result.IsSuccess && request?.Token == null)
            RefreshTokenCookie.Delete(Response);
        return result.ToActionResult(this);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await authService.LogoutAsync(User.GetUserId(), cancellationToken);
        RefreshTokenCookie.Delete(Response);
        return result.ToActionResult(this);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        var result = await passwordService.ChangePasswordAsync(User.GetUserId(), dto, cancellationToken);
        if (result.IsSuccess)
            RefreshTokenCookie.Delete(Response);
        return result.ToActionResult(this);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto, CancellationToken cancellationToken)
        => (await passwordService.ForgotPasswordAsync(dto, cancellationToken)).ToActionResult(this);

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto, CancellationToken cancellationToken)
        => (await passwordService.ResetPasswordAsync(dto, cancellationToken)).ToActionResult(this);

    [Authorize]
    [HttpPost("2fa-enable")]
    public async Task<IActionResult> EnableTwoFactor(CancellationToken cancellationToken)
        => (await authService.EnableTwoFactorAuthenticationAsync(User.GetUserId(), cancellationToken)).ToActionResult(this);

    [Authorize]
    [HttpPost("2fa-disable")]
    public async Task<IActionResult> DisableTwoFactor(CancellationToken cancellationToken)
        => (await authService.DisableTwoFactorAuthenticationAsync(User.GetUserId(), cancellationToken)).ToActionResult(this);

    [HttpPost("2fa/send-code")]
    public async Task<IActionResult> SendTwoFactorCode(TwoFactorSendCodeDto dto, CancellationToken cancellationToken)
        => (await authService.SendTwoFactorCodeAsync(dto, cancellationToken)).ToActionResult(this);

    [HttpPost("2fa-verify")]
    public async Task<IActionResult> VerifyTwoFactor(VerifyTwoFactorDto dto, CancellationToken cancellationToken)
        => WithRefreshCookie(await authService.VerifyTwoFactorAuthenticationAsync(dto, cancellationToken));

    private IActionResult WithRefreshCookie(Result<AuthDto> result)
    {
        if (result.IsSuccess)
            RefreshTokenCookie.Append(Response, result.Data!);
        return result.ToActionResult(this);
    }
}

/// <summary>Token to revoke; when omitted, the refresh token cookie of this client is revoked.</summary>
public sealed record RevokeTokenRequest(string? Token);
