using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Services.Auth;

public class PasswordService(UserManager<ApplicationUser> userManager, IAppDbContext db, IAppEmailService appEmailService) : IPasswordService
{
    public async Task<Result> ChangePasswordAsync(string userId, ChangePasswordDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.NotFound("User Not Found", ErrorCodes.UserNotFound);

        var result = await userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            return Result.Validation(result.ErrorText());

        await SignOutEverywhereAsync(user.Id, cancellationToken);
        await appEmailService.SendPasswordChangedEmailAsync(user, cancellationToken);
        return Result.Success("Password Changed Successfully");
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken cancellationToken = default)
    {
        // Same answer whether or not the email exists, so accounts can't be enumerated
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user != null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await appEmailService.SendResetPasswordEmailAsync(user, token, cancellationToken);
        }
        return Result.Success("Reset password email sent");
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return Result.Validation("Invalid token");

        var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
            return Result.Validation(result.ErrorText());

        await SignOutEverywhereAsync(user.Id, cancellationToken);
        await appEmailService.SendPasswordChangedEmailAsync(user, cancellationToken);
        return Result.Success("Password reset successfully");
    }

    private Task<int> SignOutEverywhereAsync(string userId, CancellationToken cancellationToken)
        => db.RefreshTokens.Where(x => x.ApplicationUserId == userId).ExecuteDeleteAsync(cancellationToken);
}
