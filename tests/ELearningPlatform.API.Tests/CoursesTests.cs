using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.Common.Results;
using ELearningPlatform.Application.DTOs.Course;
using ELearningPlatform.Application.DTOs.React;

namespace ELearningPlatform.API.Tests;

public class CoursesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_AsTeacher_Returns201WithLocationAndBody()
    {
        var teacher = await factory.CreateUserAsync("Teacher");

        var response = await teacher.Client.PostJsonAsync("api/Courses", new { name = "Algebra", description = "Numbers", hours = 12, price = 15.5m });

        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        var course = await response.ReadAsync<CourseDto>();
        Assert.Equal("Algebra", course.Name);
        Assert.Equal(12, course.Hours);
        Assert.Equal("Test User", course.TeacherName);
        Assert.EndsWith($"/api/Courses/{course.Id}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AsAdminWithoutTeacherProfile_Returns403()
    {
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.PostJsonAsync("api/Courses", new { name = "Admin course", hours = 1, price = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("", 10, 5)]
    [InlineData("A", 10, 5)]
    [InlineData("Valid name", 0, 5)]
    [InlineData("Valid name", 10001, 5)]
    [InlineData("Valid name", 10, -1)]
    [InlineData("Valid name", 10, 1000001)]
    public async Task Create_InvalidBody_Returns400(string name, int hours, decimal price)
    {
        var teacher = await factory.CreateUserAsync("Teacher");

        var response = await teacher.Client.PostJsonAsync("api/Courses", new { name, hours, price });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEmpty(await response.ReadValidationErrorKeysAsync());
    }

    [Fact]
    public async Task DecimalPrice_RoundTripsExactly()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var created = await teacher.CreateCourseAsync(price: 49.99m);

        var course = await (await teacher.Client.GetAsync($"api/Courses/{created.Id}")).ReadAsync<CourseDto>();

        Assert.Equal(49.99m, course.Price);
        Assert.False(course.IsFree);
        var raw = await (await teacher.Client.GetAsync($"api/Courses/{created.Id}")).Content.ReadAsStringAsync();
        Assert.Contains("\"price\":49.99", raw);
        Assert.Contains("\"isFree\":false", raw);
    }

    [Fact]
    public async Task FreeCourse_IsFreeTrue()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var created = await teacher.CreateCourseAsync(price: 0m);

        var raw = await (await teacher.Client.GetAsync($"api/Courses/{created.Id}")).Content.ReadAsStringAsync();

        Assert.Contains("\"isFree\":true", raw);
    }

    [Fact]
    public async Task Enums_AreSerializedAsStrings()
    {
        var teacher = await factory.CreateUserAsync("Teacher", specialization: "Chemistry");
        var created = await teacher.CreateCourseAsync();

        var raw = await (await teacher.Client.GetAsync($"api/Courses/{created.Id}")).Content.ReadAsStringAsync();

        Assert.Contains("\"specialization\":\"Chemistry\"", raw);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404ProblemWithCode()
    {
        var response = await factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Student")).GetAsync("api/Courses/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Course.NotFound", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Update_ChangesOnlyProvidedFields()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var created = await teacher.CreateCourseAsync(name: "Original", hours: 5);

        var update = await teacher.Client.PutJsonAsync($"api/Courses/{created.Id}", new { hours = 40 });

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, update);
        var course = await (await teacher.Client.GetAsync($"api/Courses/{created.Id}")).ReadAsync<CourseDto>();
        Assert.Equal("Original", course.Name);
        Assert.Equal(40, course.Hours);
    }

    [Fact]
    public async Task UpdatePrice_ChangesPrice()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var created = await teacher.CreateCourseAsync(price: 10m);

        var patch = await teacher.Client.PatchJsonAsync($"api/Courses/{created.Id}", new { newPrice = 0.5m });

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, patch);
        Assert.Equal(0.5m, (await (await teacher.Client.GetAsync($"api/Courses/{created.Id}")).ReadAsync<CourseDto>()).Price);
    }

    [Fact]
    public async Task UpdatePrice_Negative_Returns400()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var created = await teacher.CreateCourseAsync();

        var patch = await teacher.Client.PatchJsonAsync($"api/Courses/{created.Id}", new { newPrice = -1m });

        Assert.Equal(HttpStatusCode.BadRequest, patch.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGet_Returns404()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var created = await teacher.CreateCourseAsync();

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await teacher.Client.DeleteAsync($"api/Courses/{created.Id}"));

        Assert.Equal(HttpStatusCode.NotFound, (await teacher.Client.GetAsync($"api/Courses/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.Client.DeleteAsync($"api/Courses/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Pagination_ReturnsAbsoluteNavigationLinks()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        for (var i = 0; i < 3; i++)
            await teacher.CreateCourseAsync();

        var page = await (await teacher.Client.GetAsync("api/Courses/CoursesPagination?pageNumber=2&pageSize=1"))
            .ReadAsync<PaginatedResult<CourseDto>>();

        Assert.Single(page.Items);
        Assert.Equal(2, page.CurrentPage);
        Assert.True(page.TotalCount >= 3);
        Assert.Equal(page.TotalCount, page.TotalPages);
        Assert.Equal("https://localhost/api/Courses/CoursesPagination?pageNumber=3&pageSize=1", page.NextPage);
        Assert.Equal("https://localhost/api/Courses/CoursesPagination?pageNumber=1&pageSize=1", page.PrevPage);
    }

    [Theory]
    [InlineData(1000, 100)]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    public async Task Pagination_ClampsPageSize(int requested, int expected)
    {
        var client = factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Student"));

        var page = await (await client.GetAsync($"api/Courses/CoursesPagination?pageNumber=1&pageSize={requested}"))
            .ReadAsync<PaginatedResult<CourseDto>>();

        Assert.Equal(expected, page.PageSize);
        Assert.Null(page.PrevPage);
    }

    [Fact]
    public async Task SearchByName_ToleratesTypos()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var marker = Guid.NewGuid().ToString("N")[..6];
        var created = await teacher.CreateCourseAsync(name: $"Quantum Zebra Mechanics {marker}");

        var results = await (await teacher.Client.GetAsync($"api/Courses/{Uri.EscapeDataString($"Quantm Zebra Mechanics {marker}")}"))
            .ReadAsync<List<CourseDto>>();

        Assert.Contains(results, c => c.Id == created.Id);
    }

    [Fact]
    public async Task SearchByName_NoMatch_ReturnsEmptyList()
    {
        var client = factory.CreateHttpsClient().WithBearer(TokenFactory.Create("Student"));

        var results = await (await client.GetAsync("api/Courses/zzzzqqqqxxxxwwww")).ReadAsync<List<CourseDto>>();

        Assert.Empty(results);
    }

    [Fact]
    public async Task FilterBySpecialization_ReturnsOnlyThatSpecialization()
    {
        var chemist = await factory.CreateUserAsync("Teacher", specialization: "Chemistry");
        var physicist = await factory.CreateUserAsync("Teacher", specialization: "Physics");
        var chemistry = await chemist.CreateCourseAsync();
        var physics = await physicist.CreateCourseAsync();

        var results = await (await chemist.Client.GetAsync("api/Courses/specialization/Chemistry")).ReadAsync<List<CourseDto>>();

        Assert.Contains(results, c => c.Id == chemistry.Id);
        Assert.DoesNotContain(results, c => c.Id == physics.Id);
        Assert.All(results, c => Assert.Equal(Domain.Enums.Specialization.Chemistry, c.Specialization));
    }

    [Fact]
    public async Task Rate_Twice_Returns409()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var student = await factory.CreateUserAsync("Student");

        var first = await student.Client.PostJsonAsync($"api/Courses/{course.Id}/Rate", new { isLike = true, rating = "FiveStars" });
        var second = await student.Client.PostJsonAsync($"api/Courses/{course.Id}/Rate", new { isLike = false, rating = "OneStar" });

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, first);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("Course.AlreadyRated", await second.ReadProblemCodeAsync());
        var dto = await (await student.Client.GetAsync($"api/Courses/{course.Id}")).ReadAsync<CourseDto>();
        Assert.Equal(1, dto.NumberOfLike);
        Assert.Equal(0, dto.NumberOfDislike);
    }

    [Fact]
    public async Task Rate_InvalidRating_Returns400()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();

        var response = await teacher.Client.PostJsonAsync($"api/Courses/{course.Id}/Rate", new { isLike = true, rating = 9 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rate_UnknownCourse_Returns404()
    {
        var student = await factory.CreateUserAsync("Student");

        var response = await student.Client.PostJsonAsync("api/Courses/987654/Rate", new { isLike = true, rating = "FiveStars" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Comments_AreListedNewestFirstWithAuthorName()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var student = await factory.CreateUserAsync("Student");

        await student.Client.PostJsonAsync($"api/Courses/{course.Id}/Comment", new { comment = "first" });
        await student.Client.PostJsonAsync($"api/Courses/{course.Id}/Comment", new { comment = "second" });
        var comments = await (await student.Client.GetAsync($"api/Courses/{course.Id}/Comments")).ReadAsync<List<CommentDto>>();

        Assert.Equal(["second", "first"], comments.Select(c => c.Comment));
        Assert.All(comments, c => Assert.Equal("Test User", c.UserName));
        Assert.Equal(2, (await (await student.Client.GetAsync($"api/Courses/{course.Id}")).ReadAsync<CourseDto>()).NumberOfComment);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Comment_Empty_Returns400(string? comment)
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();

        var response = await teacher.Client.PostJsonAsync($"api/Courses/{course.Id}/Comment", new { comment });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ContainsCreatedCourse()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();

        var all = await (await teacher.Client.GetAsync("api/Courses")).ReadAsync<List<CourseDto>>();

        Assert.Contains(all, c => c.Id == course.Id);
    }
}

/// <summary>Own factory so "no ratings yet" is a known starting state.</summary>
public class TopRatedCourseTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task TopRated_NotFoundUntilRated_ThenHighestAverageWins()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var good = await teacher.CreateCourseAsync();
        var bad = await teacher.CreateCourseAsync();

        var none = await teacher.Client.GetAsync("api/Courses/TopRated");
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);

        var s1 = await factory.CreateUserAsync("Student");
        var s2 = await factory.CreateUserAsync("Student");
        await s1.Client.PostJsonAsync($"api/Courses/{good.Id}/Rate", new { isLike = true, rating = "FiveStars" });
        await s2.Client.PostJsonAsync($"api/Courses/{good.Id}/Rate", new { isLike = true, rating = "FourStars" });
        await s1.Client.PostJsonAsync($"api/Courses/{bad.Id}/Rate", new { isLike = false, rating = "OneStar" });

        var top = await (await teacher.Client.GetAsync("api/Courses/TopRated")).ReadAsync<CourseDto>();
        Assert.Equal(good.Id, top.Id);
    }
}
