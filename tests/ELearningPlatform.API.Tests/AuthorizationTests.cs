using System.Net;
using System.Net.Http.Headers;
using ELearningPlatform.API.Tests.Infrastructure;

namespace ELearningPlatform.API.Tests;

public class AuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    public static TheoryData<string, string> ProtectedEndpoints => new()
    {
        { "GET", "api/Courses" },
        { "GET", "api/Courses/1" },
        { "POST", "api/Courses" },
        { "GET", "api/courses/1/Exams" },
        { "GET", "api/Exam/1/Questions" },
        { "GET", "api/Courses/1/Videos" },
        { "GET", "api/Users" },
        { "GET", "api/Users/Profile" },
        { "POST", "api/Notification/send-sms" },
        { "POST", "api/Account/logout" },
        { "POST", "api/Account/change-password" },
        { "POST", "api/Account/2fa-enable" },
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task Anonymous_Gets401(string method, string url)
    {
        var response = await factory.CreateHttpsClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static TheoryData<string, string> TeacherOrAdminEndpoints => new()
    {
        { "POST", "api/Courses" },
        { "DELETE", "api/Courses/1" },
        { "PATCH", "api/Courses/1" },
        { "POST", "api/courses/1/Exams" },
        { "DELETE", "api/courses/1/Exams/1" },
        { "GET", "api/Exam/1/Questions" },
        { "POST", "api/Exam/1/Questions/TF_Question" },
        { "DELETE", "api/Courses/1/Videos/1" },
    };

    public static TheoryData<string, string> AdminOnlyEndpoints => new()
    {
        { "GET", "api/Users" },
        { "GET", "api/Users/Teachers" },
        { "GET", "api/Users/Students" },
        { "PATCH", "api/Users/x/lock" },
        { "POST", "api/Users/add-role" },
        { "POST", "api/Notification/send-sms" },
        { "POST", "api/Notification/send-email" },
    };

    [Theory]
    [MemberData(nameof(TeacherOrAdminEndpoints))]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public async Task Student_Gets403OnTeacherAndAdminEndpoints(string method, string url)
    {
        var client = factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Student"));

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public async Task Teacher_Gets403OnAdminEndpoints(string method, string url)
    {
        var client = factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Teacher"));

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("api/Users")]
    [InlineData("api/Users/Teachers")]
    [InlineData("api/Users/Students")]
    [InlineData("api/Courses")]
    public async Task Admin_CanReadAdminAndSharedEndpoints(string url)
    {
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.GetAsync(url);

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task Admin_CanReadExamQuestions()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var exam = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddHours(1));
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.GetAsync($"api/Exam/{exam.Id}/Questions");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task Admin_CanDeleteATeachersCourse()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.DeleteAsync($"api/Courses/{course.Id}");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_Gets401()
    {
        var forged = TokenFactory.Create("Admin", key: "another-signing-key-that-is-long-enough-0123456789abcdef");

        var response = await factory.CreateHttpsClient().WithBearer(forged).GetAsync("api/Users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Gets401()
    {
        var expired = TokenFactory.Create("Admin", expires: DateTime.UtcNow.AddMinutes(-5));

        var response = await factory.CreateHttpsClient().WithBearer(expired).GetAsync("api/Users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("https://evil-issuer/", ApiFactory.Audience)]
    [InlineData(ApiFactory.Issuer, "https://evil-audience/")]
    public async Task WrongIssuerOrAudience_Gets401(string issuer, string audience)
    {
        var token = TokenFactory.Create("Admin", issuer: issuer, audience: audience);

        var response = await factory.CreateHttpsClient().WithBearer(token).GetAsync("api/Users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedBearer_Gets401()
    {
        var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync("api/Courses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidHandMadeToken_IsAccepted()
    {
        var response = await factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Student")).GetAsync("api/Courses");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task AccessTokenOfUserPromotedToTeacher_StillHasOldRoleUntilRefreshed()
    {
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();
        await admin.Client.PostJsonAsync("api/Users/add-role", new { userId = student.UserId, role = "Teacher" });

        var before = await student.Client.GetAsync("api/Exam/1/Questions");
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);

        var refreshed = await (await student.Client.PostAsync("api/Account/refresh-token", null))
            .ReadAsync<Application.DTOs.Authentication.AuthDto>();
        var after = await factory.CreateHttpsClient().WithBearer(refreshed.AccessToken).GetAsync("api/Exam/999999/Questions");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }
}
