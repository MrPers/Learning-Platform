using Microsoft.AspNetCore.Identity;

namespace ELearningPlatform.Application.Services.Auth;

public class ExternalLoginService(
    UserManager<ApplicationUser> userManager,
    IGoogleTokenValidator googleTokenValidator,
    AuthTokenIssuer tokenIssuer) : IExternalLoginService
{
    public const string GoogleProvider = "Google";

    public async Task<Result<AuthDto>> GoogleLoginAsync(ExternalLoginDto dto, CancellationToken cancellationToken = default)
    {
        var google = await googleTokenValidator.ValidateAsync(dto.IdToken, cancellationToken);
        if (google == null)
            return Result<AuthDto>.Unauthorized("Invalid Google token", ErrorCodes.InvalidExternalToken);

        // An unverified Google email could belong to someone else, so it must not be linked to an account
        if (!google.EmailVerified)
            return Result<AuthDto>.Unauthorized("Google account email is not verified", ErrorCodes.InvalidExternalToken);

        var user = await userManager.FindByLoginAsync(GoogleProvider, google.Subject)
                   ?? await userManager.FindByEmailAsync(google.Email);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = Guid.NewGuid().ToString(),
                Email = google.Email,
                EmailConfirmed = true,
                FirstName = google.GivenName ?? string.Empty,
                LastName = google.FamilyName ?? string.Empty,
            };
            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
                return Result<AuthDto>.Fail($"Failed to create user\nError:{result.ErrorText()}");

            var roleResult = await userManager.AddToRoleAsync(user, RolesConstants.Student);
            if (!roleResult.Succeeded)
                return Result<AuthDto>.Fail("Failed to assign role to user");
        }
        else if (await userManager.IsLockedOutAsync(user))
        {
            return Result<AuthDto>.Forbidden("Your account is locked. Please try again later.", ErrorCodes.LockedOut);
        }

        var logins = await userManager.GetLoginsAsync(user);
        if (!logins.Any(l => l.LoginProvider == GoogleProvider))
            await userManager.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, google.Subject, GoogleProvider));

        return Result<AuthDto>.Success(await tokenIssuer.IssueAsync(user, cancellationToken));
    }
}
