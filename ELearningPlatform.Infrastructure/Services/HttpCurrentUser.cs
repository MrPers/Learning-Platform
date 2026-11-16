using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ELearningPlatform.Infrastructure.Services;

/// <summary>The authenticated caller of the current HTTP request (JWT "sub" claim); empty in background jobs.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? TraceId => Activity.Current?.TraceId.ToString() ?? accessor.HttpContext?.TraceIdentifier;
}
