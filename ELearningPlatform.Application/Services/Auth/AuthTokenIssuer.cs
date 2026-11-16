using Microsoft.AspNetCore.Identity;

namespace ELearningPlatform.Application.Services.Auth;

/// <summary>Issues an access token plus a new refresh token (stored hashed) for a signed-in user.</summary>
public sealed class AuthTokenIssuer(UserManager<ApplicationUser> userManager, IAppDbContext db, IJwtTokenService jwtTokenService)
{
    public async Task<AuthDto> IssueAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var jwt = await jwtTokenService.GenerateTokenAsync(user, cancellationToken);
        var refreshToken = jwtTokenService.GenerateRefreshToken(user);
        db.RefreshTokens.Add(refreshToken.Entity);
        await db.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(user);
        return new AuthDto
        {
            AccessToken = jwt.AccessToken,
            ExpiresOn = jwt.ExpiresAt,
            UserId = user.Id,
            Role = roles.FirstOrDefault() ?? RolesConstants.Student,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiration = refreshToken.Entity.ExpiresOn,
        };
    }
}
