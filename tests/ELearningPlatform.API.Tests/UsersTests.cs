using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.DTOs.User;

namespace ELearningPlatform.API.Tests;

public class UsersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Admin_ListsUsersTeachersAndStudents()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();

        var users = await (await admin.Client.GetAsync("api/Users")).ReadAsync<List<UserDto>>();
        var teachers = await (await admin.Client.GetAsync("api/Users/Teachers")).ReadAsync<List<TeacherDto>>();
        var students = await (await admin.Client.GetAsync("api/Users/Students")).ReadAsync<List<StudentDto>>();

        Assert.Contains(users, u => u.Id == teacher.UserId);
        Assert.Contains(users, u => u.Id == student.UserId);
        Assert.Contains(teachers, t => t.Id == teacher.UserId);
        Assert.DoesNotContain(teachers, t => t.Id == student.UserId);
        Assert.Contains(students, s => s.Id == student.UserId && s.Level == Domain.Enums.Level.Beginner);
    }

    [Fact]
    public async Task Admin_GetsUserById()
    {
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();

        var user = await (await admin.Client.GetAsync($"api/Users/{student.UserId}")).ReadAsync<UserDto>();

        Assert.Equal(student.Email, user.Email);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.GetAsync($"api/Users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("User.NotFound", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Lock_BlocksLoginAndRefresh_UnlockRestoresLogin()
    {
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await admin.Client.PatchAsync($"api/Users/{student.UserId}/lock", null));

        var login = await factory.CreateHttpsClient().LoginAsync(student.Email, student.Password, "Student");
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("Auth.LockedOut", await login.ReadProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await student.Client.PostAsync("api/Account/refresh-token", null)).StatusCode);

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await admin.Client.PatchAsync($"api/Users/{student.UserId}/unlock", null));
        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await factory.CreateHttpsClient().LoginAsync(student.Email, student.Password, "Student"));
    }

    [Fact]
    public async Task Lock_Admin_Returns403()
    {
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.PatchAsync($"api/Users/{admin.UserId}/lock", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Lock_UnknownUser_Returns404()
    {
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.PatchAsync($"api/Users/{Guid.NewGuid()}/lock", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddRole_ThenAgain_Conflicts_ThenRemove()
    {
        var student = await factory.CreateUserAsync("Student");
        var admin = await factory.LoginAdminAsync();
        var body = new { userId = student.UserId, role = "Teacher" };

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await admin.Client.PostJsonAsync("api/Users/add-role", body));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Client.PostJsonAsync("api/Users/add-role", body)).StatusCode);
        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await admin.Client.DeleteJsonAsync("api/Users/remove-role", body));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Client.DeleteJsonAsync("api/Users/remove-role", body)).StatusCode);
    }

    [Fact]
    public async Task RemoveAdminRole_Returns403()
    {
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.DeleteJsonAsync("api/Users/remove-role", new { userId = admin.UserId, role = "Admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("short", "Teacher")]
    [InlineData("", "Teacher")]
    [InlineData("00000000-0000-0000-0000-000000000000", "Janitor")]
    public async Task AddRole_InvalidBody_Returns400(string userId, string role)
    {
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.PostJsonAsync("api/Users/add-role", new { userId, role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ProfileGetAndUpdate()
    {
        var admin = await factory.LoginAdminAsync();

        await ResponseAssert.StatusAsync(HttpStatusCode.OK,
            await admin.Client.PutJsonAsync("api/Users/Profile", new { firstName = "Super", lastName = "Admin" }));
        var profile = await (await admin.Client.GetAsync("api/Users/Profile")).ReadAsync<UserDto>();

        Assert.Equal(("Super", "Admin"), (profile.FirstName, profile.LastName));
        Assert.Equal(ApiFactory.AdminEmail, profile.Email);
    }

    [Fact]
    public async Task UpdateProfile_InvalidNames_Returns400()
    {
        var admin = await factory.LoginAdminAsync();

        var response = await admin.Client.PutJsonAsync("api/Users/Profile", new { firstName = "A", lastName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_Png_IsStored()
    {
        var admin = await factory.LoginAdminAsync();
        var form = new MultipartFormDataContent { { TestData.File(TestData.Png, "image/png"), "PictureFile", "me.png" } };

        var response = await admin.Client.PatchAsync("api/Users/Profile/Picture", form);

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
        var profile = await (await admin.Client.GetAsync("api/Users/Profile")).ReadAsync<UserDto>();
        Assert.EndsWith(".png", profile.ProfilePictureUrl);
        Assert.True(File.Exists(Path.Combine(factory.WebRoot, "Uploads", "Images", profile.ProfilePictureUrl!)));
    }

    [Fact]
    public async Task ProfilePicture_FakePng_Returns400()
    {
        var admin = await factory.LoginAdminAsync();
        var form = new MultipartFormDataContent { { TestData.File(TestData.Exe, "image/png"), "PictureFile", "me.png" } };

        var response = await admin.Client.PatchAsync("api/Users/Profile/Picture", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    public async Task NonAdmin_CanReadOwnProfile(string role)
    {
        var user = await factory.CreateUserAsync(role);

        var response = await user.Client.GetAsync("api/Users/Profile");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
    }
}
