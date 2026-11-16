namespace ELearningPlatform.API.Extensions;

/// <summary>The refresh token travels only in an HttpOnly cookie, out of reach of page scripts.</summary>
public static class RefreshTokenCookie
{
    public const string Name = "refreshToken";
    public const string Path = "/api/Account";

    public static void Append(HttpResponse response, AuthDto auth)
        => response.Cookies.Append(Name, auth.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(auth.RefreshTokenExpiration, DateTimeKind.Utc)),
        });

    public static void Delete(HttpResponse response)
        => response.Cookies.Delete(Name, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = Path });

    public static string? Read(HttpRequest request) => request.Cookies[Name];
}
