using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ELearningPlatform.Application.Settings;
using ELearningPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ELearningPlatform.API.Extensions;

public static class RateLimitPolicies
{
    /// <summary>Per-IP limit for anonymous auth endpoints (login, 2FA, password reset, registration).</summary>
    public const string Auth = "auth";

    /// <summary>Per-user limit for endpoints that move money (top-ups, purchases, payouts, refunds).</summary>
    public const string Payments = "payments";
}

public static class ApiServiceCollectionExtensions
{
    public const string CorsSection = "Cors";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(options => options.Filters.Add<FluentValidationFilter>())
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi();

        services.AddJwtAuthentication();
        services.AddAuthorizationBuilder()
            .AddPolicy(PoliciesConstants.AdminOnly, p => p.RequireRole(RolesConstants.Admin))
            .AddPolicy(PoliciesConstants.AdminTeacher, p => p.RequireRole(RolesConstants.Admin, RolesConstants.Teacher))
            .AddPolicy(PoliciesConstants.AdminTeacherStudent, p => p.RequireRole(RolesConstants.Admin, RolesConstants.Teacher, RolesConstants.Student))
            .AddPolicy(PoliciesConstants.TeacherOnly, p => p.RequireRole(RolesConstants.Teacher))
            .AddPolicy(PoliciesConstants.StudentOnly, p => p.RequireRole(RolesConstants.Student));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            options.AddPolicy(RateLimitPolicies.Payments, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("RateLimiting:PaymentsPermitLimit", 20),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        var origins = configuration.GetSection($"{CorsSection}:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(AppSettingsConstants.CORSPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from the validated JWT options, so the same settings sign and validate tokens
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JWTSettings>>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Value.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt.Value.SecretKey),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = JwtTokenService.RoleClaim,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
    }
}
