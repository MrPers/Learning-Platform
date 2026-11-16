using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ELearningPlatform.Infrastructure.Tests.Services;

public class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _time = new(Now);
    private readonly UserManager<ApplicationUser> _userManager =
        Substitute.For<UserManager<ApplicationUser>>(Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

    private readonly ApplicationUser _user = new() { Id = "user-1", Email = "jane@elearning.test", UserName = "jane" };

    private JwtTokenService CreateService(JWTSettings? settings = null)
        => new(_userManager, Options.Create(settings ?? TestSettings.Jwt()), _time);

    private void GivenRoles(params string[] roles) => _userManager.GetRolesAsync(_user).Returns(roles.ToList());

    private static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

    private static TokenValidationParameters ValidationParameters(string key, DateTime now) => new()
    {
        ValidIssuer = TestSettings.Issuer,
        ValidAudience = TestSettings.Audience,
        IssuerSigningKey = JwtTokenService.CreateSigningKey(key),
        LifetimeValidator = (notBefore, expires, _, _) => notBefore <= now && expires > now,
    };

    [Fact]
    public async Task GenerateToken_ContainsSubjectEmailAndJti()
    {
        GivenRoles(RolesConstants.Student);
        var jwt = await CreateService().GenerateTokenAsync(_user);

        var token = Read(jwt.AccessToken);
        Assert.Equal("user-1", token.Subject);
        Assert.Equal("jane@elearning.test", token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.True(Guid.TryParse(token.Id, out _));
    }

    [Fact]
    public async Task GenerateToken_SetsIssuerAndAudience()
    {
        GivenRoles();
        var token = Read((await CreateService().GenerateTokenAsync(_user)).AccessToken);

        Assert.Equal(TestSettings.Issuer, token.Issuer);
        Assert.Contains(TestSettings.Audience, token.Audiences);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Admin,Teacher")]
    [InlineData("Admin,Teacher,Student")]
    public async Task GenerateToken_ContainsEveryRole(string roleList)
    {
        var roles = roleList.Split(',');
        GivenRoles(roles);
        var token = Read((await CreateService().GenerateTokenAsync(_user)).AccessToken);

        var tokenRoles = token.Claims.Where(c => c.Type == JwtTokenService.RoleClaim).Select(c => c.Value).ToArray();
        Assert.Equal(roles.OrderBy(r => r), tokenRoles.OrderBy(r => r));
    }

    [Fact]
    public async Task GenerateToken_WithoutRoles_HasNoRoleValues()
    {
        GivenRoles();
        var token = Read((await CreateService().GenerateTokenAsync(_user)).AccessToken);

        Assert.DoesNotContain(token.Claims, c => c.Type == JwtTokenService.RoleClaim && c.Value.Length > 0);
    }

    [Fact]
    public async Task GenerateToken_WithoutEmail_UsesEmptyEmailClaim()
    {
        _user.Email = null;
        GivenRoles();
        var token = Read((await CreateService().GenerateTokenAsync(_user)).AccessToken);

        Assert.Equal(string.Empty, token.GetClaim(JwtRegisteredClaimNames.Email).Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(1440)]
    public async Task GenerateToken_ExpiresAfterConfiguredMinutes(double minutes)
    {
        GivenRoles();
        var jwt = await CreateService(TestSettings.Jwt(minutes)).GenerateTokenAsync(_user);

        var expected = Now.UtcDateTime.AddMinutes(minutes);
        Assert.Equal(expected, jwt.ExpiresAt);
        Assert.Equal(expected, Read(jwt.AccessToken).ValidTo);
    }

    [Fact]
    public async Task GenerateToken_IssuedAtAndNotBeforeUseTimeProvider()
    {
        GivenRoles();
        var token = Read((await CreateService().GenerateTokenAsync(_user)).AccessToken);

        Assert.Equal(Now.UtcDateTime, token.IssuedAt);
        Assert.Equal(Now.UtcDateTime, token.ValidFrom);
    }

    [Fact]
    public async Task GenerateToken_IsSignedWithHmacSha256()
    {
        GivenRoles();
        var token = Read((await CreateService().GenerateTokenAsync(_user)).AccessToken);

        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Alg);
    }

    [Fact]
    public async Task GenerateToken_ValidatesWithConfiguredKey()
    {
        GivenRoles(RolesConstants.Teacher);
        var jwt = await CreateService().GenerateTokenAsync(_user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(jwt.AccessToken, ValidationParameters(TestSettings.JwtSecret, Now.UtcDateTime.AddMinutes(1)));

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task GenerateToken_FailsValidationWithDifferentKey()
    {
        GivenRoles();
        var jwt = await CreateService().GenerateTokenAsync(_user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(jwt.AccessToken,
            ValidationParameters("another-secret-key-that-is-long-enough-for-hmac!!", Now.UtcDateTime.AddMinutes(1)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GenerateToken_FailsValidationAfterExpiry()
    {
        GivenRoles();
        var jwt = await CreateService(TestSettings.Jwt(minutes: 5)).GenerateTokenAsync(_user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(jwt.AccessToken, ValidationParameters(TestSettings.JwtSecret, Now.UtcDateTime.AddMinutes(6)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GenerateToken_EachTokenHasUniqueJti()
    {
        GivenRoles();
        var service = CreateService();

        var first = Read((await service.GenerateTokenAsync(_user)).AccessToken).Id;
        var second = Read((await service.GenerateTokenAsync(_user)).AccessToken).Id;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GenerateRefreshToken_StoresOnlyTheHash()
    {
        var issued = CreateService().GenerateRefreshToken(_user);

        Assert.NotEqual(issued.Token, issued.Entity.TokenHash);
        Assert.Equal(CreateService().HashRefreshToken(issued.Token), issued.Entity.TokenHash);
        Assert.DoesNotContain(issued.Token, issued.Entity.TokenHash);
    }

    [Fact]
    public void GenerateRefreshToken_BelongsToUser()
    {
        var issued = CreateService().GenerateRefreshToken(_user);

        Assert.Equal("user-1", issued.Entity.ApplicationUserId);
        Assert.Null(issued.Entity.RevokedAt);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(365)]
    public void GenerateRefreshToken_ExpiresAfterConfiguredDays(int days)
    {
        var issued = CreateService(TestSettings.Jwt(days: days)).GenerateRefreshToken(_user);

        Assert.Equal(Now.UtcDateTime, issued.Entity.CreatedAt);
        Assert.Equal(Now.UtcDateTime.AddDays(days), issued.Entity.ExpiresOn);
        Assert.True(issued.Entity.IsActive(Now.UtcDateTime));
        Assert.False(issued.Entity.IsActive(Now.UtcDateTime.AddDays(days)));
    }

    [Fact]
    public void GenerateRefreshToken_IsUrlSafeBase64Of64Bytes()
    {
        var token = CreateService().GenerateRefreshToken(_user).Token;

        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.Equal(86, token.Length); // 64 bytes, base64url without padding
    }

    [Fact]
    public void GenerateRefreshToken_ProducesUniqueTokens()
    {
        var service = CreateService();
        var tokens = Enumerable.Range(0, 200).Select(_ => service.GenerateRefreshToken(_user).Token).ToHashSet();

        Assert.Equal(200, tokens.Count);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("a-very-long-refresh-token-value_with-symbols")]
    public void HashRefreshToken_IsDeterministicUppercaseHexSha256(string value)
    {
        var service = CreateService();

        var hash = service.HashRefreshToken(value);

        Assert.Equal(hash, service.HashRefreshToken(value));
        Assert.Matches("^[0-9A-F]{64}$", hash);
    }

    [Fact]
    public void HashRefreshToken_KnownVector()
        => Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", CreateService().HashRefreshToken("abc"));

    [Fact]
    public void HashRefreshToken_DifferentInputsGiveDifferentHashes()
    {
        var service = CreateService();
        Assert.NotEqual(service.HashRefreshToken("token-a"), service.HashRefreshToken("token-b"));
    }

    [Fact]
    public void CreateSigningKey_UsesUtf8BytesOfSecret()
    {
        var key = JwtTokenService.CreateSigningKey(TestSettings.JwtSecret);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(TestSettings.JwtSecret), key.Key);
    }
}
