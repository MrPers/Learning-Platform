using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ELearningPlatform.Application.Services.Auth;

public partial class AuthService(
    UserManager<ApplicationUser> userManager,
    IAppDbContext db,
    IJwtTokenService jwtTokenService,
    AuthTokenIssuer tokenIssuer,
    IAppEmailService emailService,
    INotificationService notificationService,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result<AuthDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return InvalidCredentials();

        // Check lockout before the password so a locked account can't be used as a password oracle
        if (await userManager.IsLockedOutAsync(user))
            return LockedOut();

        if (!await userManager.CheckPasswordAsync(user, dto.Password))
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
            {
                LogLockedOut(user.Id);
                return LockedOut();
            }
            return InvalidCredentials();
        }
        await userManager.ResetAccessFailedCountAsync(user);

        if (!await userManager.IsEmailConfirmedAsync(user))
            return Result<AuthDto>.Forbidden("Please confirm your email first", ErrorCodes.EmailNotConfirmed);

        if (!await userManager.IsInRoleAsync(user, dto.Role.ToString()))
            return Result<AuthDto>.Forbidden("Invalid Role", ErrorCodes.InvalidRole);

        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            var code = await userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
            await emailService.SendTwoFactorEmailAsync(user, code, cancellationToken);
            return Result<AuthDto>.Unauthorized("2FA required. Code sent to email.", ErrorCodes.TwoFactorRequired);
        }

        return Result<AuthDto>.Success(await tokenIssuer.IssueAsync(user, cancellationToken));
    }

    public async Task<Result> LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        await db.RefreshTokens.Where(x => x.ApplicationUserId == userId).ExecuteDeleteAsync(cancellationToken);
        return Result.Success("Logout Successfully");
    }

    public async Task<Result<AuthDto>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return Result<AuthDto>.Unauthorized("Refresh token is missing", ErrorCodes.RefreshTokenMissing);

        var hash = jwtTokenService.HashRefreshToken(refreshToken);
        var currentToken = await db.RefreshTokens
            .Include(x => x.ApplicationUser)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (currentToken == null)
            return InvalidRefreshToken();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!currentToken.IsActive(now))
        {
            if (currentToken.RevokedAt != null)
            {
                // A rotated-out token came back: it was probably stolen, so end every session of this user
                LogRefreshTokenReuse(currentToken.ApplicationUserId);
                await RevokeAllAsync(currentToken.ApplicationUserId, now, cancellationToken);
            }
            return InvalidRefreshToken();
        }

        var user = currentToken.ApplicationUser;
        if (await userManager.IsLockedOutAsync(user))
            return LockedOut();

        currentToken.RevokedAt = now;
        return Result<AuthDto>.Success(await tokenIssuer.IssueAsync(user, cancellationToken));
    }

    public async Task<Result> RevokeTokenAsync(string userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return Result.Validation("Refresh token is missing", ErrorCodes.RefreshTokenMissing);

        var hash = jwtTokenService.HashRefreshToken(refreshToken);
        var currentToken = await db.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.ApplicationUserId == userId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (currentToken == null || !currentToken.IsActive(now))
            return Result.Validation("Invalid or inactive refresh token", ErrorCodes.InvalidRefreshToken);

        currentToken.RevokedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Token Revoked Successfully");
    }

    public Task<Result> EnableTwoFactorAuthenticationAsync(string userId, CancellationToken cancellationToken = default)
        => SetTwoFactorAsync(userId, enabled: true);

    public Task<Result> DisableTwoFactorAuthenticationAsync(string userId, CancellationToken cancellationToken = default)
        => SetTwoFactorAsync(userId, enabled: false);

    public async Task<Result<AuthDto>> VerifyTwoFactorAuthenticationAsync(VerifyTwoFactorDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return Result<AuthDto>.Unauthorized("Invalid verification code", ErrorCodes.InvalidTwoFactorCode);

        if (await userManager.IsLockedOutAsync(user))
            return LockedOut();

        // Wrong codes count as failed logins, so the short code can't be brute-forced
        if (!await userManager.VerifyTwoFactorTokenAsync(user, ProviderFor(dto.Method), dto.Code))
        {
            await userManager.AccessFailedAsync(user);
            return Result<AuthDto>.Unauthorized("Invalid verification code", ErrorCodes.InvalidTwoFactorCode);
        }
        await userManager.ResetAccessFailedCountAsync(user);

        return Result<AuthDto>.Success(await tokenIssuer.IssueAsync(user, cancellationToken));
    }

    public async Task<Result> SendTwoFactorCodeAsync(TwoFactorSendCodeDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return Result.NotFound("User not found", ErrorCodes.UserNotFound);

        if (!await userManager.GetTwoFactorEnabledAsync(user))
            return Result.Validation("Two-factor authentication is not enabled for this account.");

        if (dto.Method == TwoFactorMethod.SMS && string.IsNullOrEmpty(user.PhoneNumber))
            return Result.Validation("No phone number is set for this account.");

        var code = await userManager.GenerateTwoFactorTokenAsync(user, ProviderFor(dto.Method));

        if (dto.Method == TwoFactorMethod.Email)
            await emailService.SendTwoFactorEmailAsync(user, code, cancellationToken);
        else
            notificationService.SendSMS(new SendSMSDto { ToPhoneNumber = user.PhoneNumber!, Body = $"Your verification code is: {code}" });

        return Result.Success("Two-factor authentication code sent successfully.");
    }

    private async Task<Result> SetTwoFactorAsync(string userId, bool enabled)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.NotFound("User Not Found", ErrorCodes.UserNotFound);
        if (user.TwoFactorEnabled == enabled)
            return Result.Conflict(enabled ? "Two Factor Already Enabled" : "Two Factor Already Disabled");

        var result = await userManager.SetTwoFactorEnabledAsync(user, enabled);
        if (!result.Succeeded)
            return Result.Fail(result.ErrorText());

        return Result.Success($"Two-factor authentication {(enabled ? "enabled" : "disabled")} successfully.");
    }

    // Through the change tracker (not ExecuteUpdate) so tokens already loaded in this scope see the revocation too
    private async Task RevokeAllAsync(string userId, DateTime now, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens
            .Where(x => x.ApplicationUserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
            token.RevokedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ProviderFor(TwoFactorMethod method)
        => method == TwoFactorMethod.Email ? TokenOptions.DefaultEmailProvider : TokenOptions.DefaultPhoneProvider;

    private static Result<AuthDto> InvalidCredentials()
        => Result<AuthDto>.Unauthorized("Email or password is incorrect", ErrorCodes.InvalidCredentials);

    private static Result<AuthDto> LockedOut()
        => Result<AuthDto>.Forbidden("Your account is locked. Please try again later.", ErrorCodes.LockedOut);

    private static Result<AuthDto> InvalidRefreshToken()
        => Result<AuthDto>.Unauthorized("Invalid or inactive refresh token", ErrorCodes.InvalidRefreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} was locked out after too many failed sign-in attempts")]
    private partial void LogLockedOut(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Revoked refresh token reused for user {UserId}; all sessions revoked")]
    private partial void LogRefreshTokenReuse(string userId);
}
