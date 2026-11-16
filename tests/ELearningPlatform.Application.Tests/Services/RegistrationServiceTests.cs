namespace ELearningPlatform.Application.Tests.Services;

public sealed class RegistrationServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IRegistrationService Sut => _ctx.Get<IRegistrationService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private static RegisterStudentDto Student(string? email = null) => new()
    {
        FirstName = "Ann", LastName = "Lee", Email = email ?? $"{Guid.NewGuid():N}@test.com",
        PhoneNumber = "+201012345678", Gender = Gender.Female, Password = ServiceTestContext.Password,
        ConfirmPassword = ServiceTestContext.Password, Level = Level.Advanced,
    };

    private static RegisterTeacherDto Teacher() => new()
    {
        FirstName = "Bob", LastName = "Ray", Email = $"{Guid.NewGuid():N}@test.com",
        PhoneNumber = "+201012345678", Gender = Gender.Male, Password = ServiceTestContext.Password,
        ConfirmPassword = ServiceTestContext.Password, Specialization = Specialization.Physics,
    };

    [Fact]
    public async Task Register_student_creates_user_profile_and_role()
    {
        var dto = Student();

        var result = await Sut.RegisterStudentAsync(dto);

        Assert.True(result.IsSuccess, result.Message);
        var user = await _ctx.Users.FindByEmailAsync(dto.Email);
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
        Assert.True(await _ctx.Users.IsInRoleAsync(user, RolesConstants.Student));
        var student = await _ctx.Db.Students.SingleAsync(s => s.ApplicationUserId == user.Id);
        Assert.Equal(Level.Advanced, student.Level);
    }

    [Fact]
    public async Task Register_teacher_creates_teacher_profile()
    {
        var dto = Teacher();

        var result = await Sut.RegisterTeacherAsync(dto);

        Assert.True(result.IsSuccess, result.Message);
        var user = await _ctx.Users.FindByEmailAsync(dto.Email);
        Assert.True(await _ctx.Users.IsInRoleAsync(user!, RolesConstants.Teacher));
        Assert.Equal(Specialization.Physics, (await _ctx.Db.Teachers.SingleAsync()).Specialization);
    }

    [Fact]
    public async Task Register_sends_a_confirmation_email()
    {
        var dto = Student();

        await Sut.RegisterStudentAsync(dto);

        await _ctx.Email.Received(1).SendConfirmEmailAsync(Arg.Is<ApplicationUser>(u => u.Email == dto.Email), Arg.Is<string>(t => t.Length > 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_with_existing_email_conflicts()
    {
        var dto = Student();
        await Sut.RegisterStudentAsync(dto);

        var result = await Sut.RegisterStudentAsync(Student(dto.Email));

        Assert.Equal(ErrorCodes.EmailAlreadyRegistered, result.Error.Code);
    }

    [Fact]
    public async Task Register_with_weak_password_fails_and_leaves_nothing_behind()
    {
        var dto = Student();
        dto.Password = dto.ConfirmPassword = "weak";

        var result = await Sut.RegisterStudentAsync(dto);

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Null(await _ctx.Users.FindByEmailAsync(dto.Email));
        Assert.False(await _ctx.Db.Students.AnyAsync());
    }

    [Fact]
    public async Task Register_with_profile_picture_stores_the_uploaded_name()
    {
        var dto = Student();
        dto.ProfilePictureFile = FormFiles.Create("me.png", FormFiles.Png);
        _ctx.Media.UploadImageAsync(dto.ProfilePictureFile, Arg.Any<CancellationToken>())
            .Returns(Result<MediaDto>.Success(new MediaDto { FakeName = "stored.png" }));

        await Sut.RegisterStudentAsync(dto);

        Assert.Equal("stored.png", (await _ctx.Users.FindByEmailAsync(dto.Email))!.ProfilePictureUrl);
    }

    [Fact]
    public async Task Register_with_rejected_picture_fails_and_rolls_back()
    {
        var dto = Student();
        dto.ProfilePictureFile = FormFiles.Create("me.png", FormFiles.Exe);
        _ctx.Media.UploadImageAsync(Arg.Any<Microsoft.AspNetCore.Http.IFormFile>(), Arg.Any<CancellationToken>())
            .Returns(Result<MediaDto>.Validation("bad image"));

        var result = await Sut.RegisterStudentAsync(dto);

        Assert.Equal(ErrorCodes.InvalidFile, result.Error.Code);
        var exists = await _ctx.InNewScopeAsync(sp => sp.GetRequiredService<AppDbContext>().Users.AnyAsync(u => u.Email == dto.Email));
        Assert.False(exists);
        await _ctx.Email.DidNotReceiveWithAnyArgs().SendConfirmEmailAsync(default!, default!, default);
    }

    [Fact]
    public async Task Confirm_email_with_valid_token_confirms_and_signs_in()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, emailConfirmed: false);
        var token = await _ctx.Users.GenerateEmailConfirmationTokenAsync(user);

        var result = await Sut.ConfirmEmailAsync(user.Email!, token);

        Assert.True(result.IsSuccess);
        Assert.True(await _ctx.Users.IsEmailConfirmedAsync(user));
        Assert.False(string.IsNullOrEmpty(result.Data!.RefreshToken));
        await _ctx.Email.Received(1).SendWelcomeEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_email_with_bad_token_fails()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, emailConfirmed: false);

        var result = await Sut.ConfirmEmailAsync(user.Email!, "garbage");

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.False(await _ctx.Users.IsEmailConfirmedAsync(user));
    }

    [Fact]
    public async Task Confirm_email_for_unknown_user_is_not_found()
    {
        var result = await Sut.ConfirmEmailAsync("x@test.com", "t");

        Assert.Equal(ErrorCodes.UserNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Resend_confirmation_sends_a_new_token()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student, emailConfirmed: false);

        var result = await Sut.ResendEmailConfirmationAsync(new ResendConfirmEmailDto { Email = user.Email! });

        Assert.True(result.IsSuccess);
        await _ctx.Email.Received(1).SendConfirmEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resend_confirmation_when_already_confirmed_conflicts()
    {
        var user = await _ctx.CreateUserAsync(RolesConstants.Student);

        var result = await Sut.ResendEmailConfirmationAsync(new ResendConfirmEmailDto { Email = user.Email! });

        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Resend_confirmation_for_unknown_email_still_succeeds_without_sending()
    {
        var result = await Sut.ResendEmailConfirmationAsync(new ResendConfirmEmailDto { Email = "x@test.com" });

        Assert.True(result.IsSuccess);
        await _ctx.Email.DidNotReceiveWithAnyArgs().SendConfirmEmailAsync(default!, default!, default);
    }
}
