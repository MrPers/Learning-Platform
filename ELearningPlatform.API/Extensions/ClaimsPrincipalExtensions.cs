using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ELearningPlatform.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The user id from the access token ("sub"; inbound claim mapping is disabled).</summary>
    public static string GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(JwtRegisteredClaimNames.Sub)
           ?? throw new InvalidOperationException("The access token has no subject claim.");
}
