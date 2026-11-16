namespace ELearningPlatform.Infrastructure.Tests.Services;

public class GoogleTokenValidatorTests
{
    private static GoogleTokenValidator CreateValidator()
        => new(Options.Create(new GoogleAuthSettings { ClientIds = ["client-1.apps.googleusercontent.com"] }), NullLogger<GoogleTokenValidator>.Instance);

    // Malformed tokens are rejected locally, before any key download
    [Theory]
    [InlineData("garbage")]
    [InlineData("a.b")]
    // SRC BUG: GoogleTokenValidator only catches InvalidJwtException; a 3-segment token whose segments are not
    // base64 JSON makes Google.Apis.Auth throw Newtonsoft JsonReaderException, which escapes (HTTP 500 instead of 401).
    [InlineData("not.a.jwt")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.")]
    public async Task ValidateAsync_MalformedToken_ReturnsNull(string token)
        => Assert.Null(await CreateValidator().ValidateAsync(token));
}
