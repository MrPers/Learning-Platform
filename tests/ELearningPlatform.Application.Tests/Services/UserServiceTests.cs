using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.Tests.Services;

public sealed class UserServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IUserService Sut => _ctx.Get<IUserService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Get_user_by_id_maps_profile()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, email: "map@test.com", firstName: "Mia", lastName: "Map");

        var dto = (await Sut.GetUserByIdAsync(user.Id)).Data!;

        Assert.Equal("map@test.com", dto.Email);
        Assert.Equal("Mia", dto.FirstName);
        Assert.Equal("Map", dto.LastName);
        Assert.Equal(Gender.Female, dto.Gender);
    }

    [Fact]
    public async Task Get_unknown_user_is_not_found()
    {
        Assert.Equal(ErrorCodes.UserNotFound, (await Sut.GetUserByIdAsync("nope")).Error.Code);
    }

    [Fact]
    public async Task Get_all_users_returns_everyone()
    {
        await _ctx.CreateUserAsync(RolesConstants.Student);
        await _ctx.CreateUserAsync(RolesConstants.Teacher);

        Assert.Equal(2, (await Sut.GetAllUsersAsync()).Data!.Count);
    }

    [Fact]
    public async Task Teachers_and_students_lists_carry_their_profile_data()
    {
        await _ctx.CreateTeacherAsync(Specialization.History);
        await _ctx.CreateStudentAsync(Level.Expert);

        var teacher = Assert.Single((await Sut.GetAllTeachersAsync()).Data!);
        var student = Assert.Single((await Sut.GetAllStudentsAsync()).Data!);

        Assert.Equal(Specialization.History, teacher.Specialization);
        Assert.Equal(Role.Teacher, teacher.Role);
        Assert.Equal(Level.Expert, student.Level);
        Assert.Equal(Role.Student, student.Role);
    }

    [Fact]
    public async Task Lock_sets_lockout_and_ends_sessions()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await _ctx.Get<IAuthService>().LoginAsync(new LoginDto { Email = user.Email!, Password = ServiceTestContext.Password, Role = Role.Student });

        var result = await Sut.LockAccountAsync(new UserActionDto { UserId = user.Id });

        Assert.True(result.IsSuccess);
        Assert.True(await _ctx.Users.IsLockedOutAsync(user));
        Assert.False(await _ctx.Db.RefreshTokens.AnyAsync(t => t.ApplicationUserId == user.Id));
    }

    [Fact]
    public async Task Locked_user_cannot_log_in()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await Sut.LockAccountAsync(new UserActionDto { UserId = user.Id });

        var login = await _ctx.Get<IAuthService>().LoginAsync(new LoginDto { Email = user.Email!, Password = ServiceTestContext.Password, Role = Role.Student });

        Assert.Equal(ErrorCodes.LockedOut, login.Error.Code);
    }

    [Fact]
    public async Task Admin_cannot_be_locked()
    {
        var admin = await _ctx.CreateUserAsync(RolesConstants.Admin);

        Assert.Equal(ErrorType.Forbidden, (await Sut.LockAccountAsync(new UserActionDto { UserId = admin.Id })).ErrorType);
        Assert.False(await _ctx.Users.IsLockedOutAsync(admin));
    }

    [Fact]
    public async Task Unlock_clears_lockout_and_failure_count()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        await Sut.LockAccountAsync(new UserActionDto { UserId = user.Id });
        await _ctx.Users.AccessFailedAsync(user);

        var result = await Sut.UnlockAccountAsync(new UserActionDto { UserId = user.Id });

        Assert.True(result.IsSuccess);
        Assert.False(await _ctx.Users.IsLockedOutAsync(user));
        Assert.Equal(0, await _ctx.Users.GetAccessFailedCountAsync(user));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lock_or_unlock_unknown_user_is_not_found(bool lockIt)
    {
        var dto = new UserActionDto { UserId = "missing" };

        var result = lockIt ? await Sut.LockAccountAsync(dto) : await Sut.UnlockAccountAsync(dto);

        Assert.Equal(ErrorCodes.UserNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Add_role_and_conflict_when_already_in_it()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var added = await Sut.AddToRoleAsync(user.Id, Role.Teacher);
        var again = await Sut.AddToRoleAsync(user.Id, Role.Teacher);

        Assert.True(added.IsSuccess);
        Assert.True(await _ctx.Users.IsInRoleAsync(user, RolesConstants.Teacher));
        Assert.Equal(ErrorType.Conflict, again.ErrorType);
    }

    [Fact]
    public async Task Remove_role()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        Assert.True((await Sut.RemoveFromRoleAsync(user.Id, Role.Student)).IsSuccess);
        Assert.False(await _ctx.Users.IsInRoleAsync(user, RolesConstants.Student));
    }

    [Fact]
    public async Task Remove_role_user_does_not_have_is_a_validation_error()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        Assert.Equal(ErrorType.Validation, (await Sut.RemoveFromRoleAsync(user.Id, Role.Teacher)).ErrorType);
    }

    [Fact]
    public async Task Admin_role_cannot_be_removed()
    {
        var admin = await _ctx.CreateUserAsync(RolesConstants.Admin);

        Assert.Equal(ErrorType.Forbidden, (await Sut.RemoveFromRoleAsync(admin.Id, Role.Admin)).ErrorType);
        Assert.True(await _ctx.Users.IsInRoleAsync(admin, RolesConstants.Admin));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Role_changes_for_unknown_user_are_not_found(bool add)
    {
        var result = add ? await Sut.AddToRoleAsync("x", Role.Student) : await Sut.RemoveFromRoleAsync("x", Role.Student);

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Update_profile_changes_only_non_empty_names()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, firstName: "Old", lastName: "Name");

        await Sut.UpdateProfileAsync(user.Id, new UpdateProfileDto { FirstName = "New", LastName = "" });

        var dto = (await Sut.GetUserByIdAsync(user.Id)).Data!;
        Assert.Equal("New", dto.FirstName);
        Assert.Equal("Name", dto.LastName);
    }

    [Fact]
    public async Task Update_profile_of_unknown_user_is_not_found()
    {
        Assert.Equal(ErrorType.NotFound, (await Sut.UpdateProfileAsync("x", new UpdateProfileDto { FirstName = "a" })).ErrorType);
    }

    [Fact]
    public async Task Update_picture_replaces_the_old_one()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        user.ProfilePictureUrl = "old.png";
        await _ctx.Users.UpdateAsync(user);
        _ctx.Media.UploadImageAsync(Arg.Any<IFormFile>(), Arg.Any<CancellationToken>()).Returns(Result<MediaDto>.Success(new MediaDto { FakeName = "new.png" }));

        var result = await Sut.UpdateProfilePictureAsync(user.Id, new UpdatePictureProfileDto { PictureFile = FormFiles.Create("p.png", FormFiles.Png) });

        Assert.True(result.IsSuccess);
        Assert.Equal("new.png", (await Sut.GetUserByIdAsync(user.Id)).Data!.ProfilePictureUrl);
        _ctx.Media.Received(1).RemoveImage("old.png");
    }

    [Fact]
    public async Task Update_picture_without_previous_picture_removes_nothing()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        user.ProfilePictureUrl = null;
        await _ctx.Users.UpdateAsync(user);
        _ctx.Media.UploadImageAsync(Arg.Any<IFormFile>(), Arg.Any<CancellationToken>()).Returns(Result<MediaDto>.Success(new MediaDto { FakeName = "new.png" }));

        await Sut.UpdateProfilePictureAsync(user.Id, new UpdatePictureProfileDto { PictureFile = FormFiles.Create("p.png", FormFiles.Png) });

        _ctx.Media.DidNotReceiveWithAnyArgs().RemoveImage(default!);
    }

    [Fact]
    public async Task Update_picture_with_rejected_upload_keeps_the_old_one()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);
        user.ProfilePictureUrl = "old.png";
        await _ctx.Users.UpdateAsync(user);
        _ctx.Media.UploadImageAsync(Arg.Any<IFormFile>(), Arg.Any<CancellationToken>()).Returns(Result<MediaDto>.Validation("bad"));

        var result = await Sut.UpdateProfilePictureAsync(user.Id, new UpdatePictureProfileDto { PictureFile = FormFiles.Create("p.png", FormFiles.Exe) });

        Assert.Equal(ErrorCodes.InvalidFile, result.Error.Code);
        Assert.Equal("old.png", (await Sut.GetUserByIdAsync(user.Id)).Data!.ProfilePictureUrl);
        _ctx.Media.DidNotReceiveWithAnyArgs().RemoveImage(default!);
    }
}
