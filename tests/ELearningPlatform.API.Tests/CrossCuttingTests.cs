using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.Services.Courses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ELearningPlatform.API.Tests;

public class ProblemDetailsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ApplicationError_IsRfc9457ProblemWithCodeAndTraceId()
    {
        var response = await factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Student")).GetAsync("api/Courses/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("type").GetString()));
        Assert.Equal("Not found", problem.GetProperty("title").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal("Course Not Found", problem.GetProperty("detail").GetString());
        Assert.Equal("Course.NotFound", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ValidationError_IsValidationProblemWithErrorsDictionary()
    {
        var response = await factory.CreateHttpsClient().PostJsonAsync("api/Account/forgot-password", new { email = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty("Email", out var errors));
        Assert.NotEmpty(errors.EnumerateArray());
    }

    [Fact]
    public async Task MalformedJson_Returns400()
    {
        var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await factory.CreateHttpsClient().PostAsync("api/Account/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        var response = await factory.CreateHttpsClient().GetAsync("api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    [InlineData("/hangfire")]
    public async Task DevelopmentOnlyEndpoints_AreNotExposedOutsideDevelopment(string url)
    {
        var response = await factory.CreateHttpsClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>A factory whose course service blows up, to see what clients get for unhandled exceptions.</summary>
public sealed class ThrowingCourseServiceFactory : ApiFactory
{
    public const string SecretMessage = "connection string Password=hunter2 leaked";

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        var courses = Substitute.For<ICourseService>();
        courses.GetAllAsync(Arg.Any<CancellationToken>()).Returns<Task<Application.Common.Results.Result<List<Application.DTOs.Course.CourseDto>>>>(
            _ => throw new InvalidOperationException(SecretMessage));
        services.RemoveAll<ICourseService>();
        services.AddSingleton(courses);
    }
}

public class UnhandledExceptionTests(ThrowingCourseServiceFactory factory) : IClassFixture<ThrowingCourseServiceFactory>
{
    [Fact]
    public async Task UnhandledException_Returns500GenericProblem_WithoutInternals()
    {
        var response = await factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Student")).GetAsync("api/Courses");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.Equal("General.Unexpected", problem.GetProperty("code").GetString());
    }
}

public sealed class RateLimitedApiFactory : ApiFactory
{
    public const int Limit = 3;

    protected override IDictionary<string, string?> Settings
    {
        get
        {
            var settings = base.Settings;
            settings["RateLimiting:AuthPermitLimit"] = Limit.ToString();
            return settings;
        }
    }
}

public class RateLimitingTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task AuthEndpoints_Return429AfterLimit_OtherEndpointsUnaffected()
    {
        var client = factory.CreateHttpsClient();

        for (var i = 0; i < RateLimitedApiFactory.Limit; i++)
        {
            var allowed = await client.LoginAsync(TestData.UniqueEmail(), "Wrong!Pass1", "Student");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var limited = await client.LoginAsync(TestData.UniqueEmail(), "Wrong!Pass1", "Student");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        // The limit belongs to the auth policy only
        var courses = await factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Student")).GetAsync("api/Courses");
        Assert.Equal(HttpStatusCode.OK, courses.StatusCode);
    }
}
