using Microsoft.AspNetCore.Identity;

namespace ELearningPlatform.Application.Services.Auth;

public class RegistrationService(
    UserManager<ApplicationUser> userManager,
    IAppDbContext db,
    IMediaService mediaService,
    AuthTokenIssuer tokenIssuer,
    IAppEmailService appEmailService) : IRegistrationService
{
    public Task<Result> RegisterTeacherAsync(RegisterTeacherDto dto, CancellationToken cancellationToken = default)
        => RegisterAsync(dto, Role.Teacher, user => db.Teachers.Add(new Teacher { ApplicationUserId = user.Id, Specialization = dto.Specialization }), cancellationToken);

    public Task<Result> RegisterStudentAsync(RegisterStudentDto dto, CancellationToken cancellationToken = default)
        => RegisterAsync(dto, Role.Student, user => db.Students.Add(new Student { ApplicationUserId = user.Id, Level = dto.Level }), cancellationToken);

    public async Task<Result<AuthDto>> ConfirmEmailAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
            return Result<AuthDto>.NotFound("User not found", ErrorCodes.UserNotFound);

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return Result<AuthDto>.Validation(result.ErrorText());

        var auth = await tokenIssuer.IssueAsync(user, cancellationToken);
        await appEmailService.SendWelcomeEmailAsync(user, cancellationToken);
        return Result<AuthDto>.Success(auth);
    }

    public async Task<Result> ResendEmailConfirmationAsync(ResendConfirmEmailDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user != null)
        {
            if (await userManager.IsEmailConfirmedAsync(user))
                return Result.Conflict("Email is already confirmed");

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await appEmailService.SendConfirmEmailAsync(user, token, cancellationToken);
        }

        return Result.Success("Confirmation email resent. Please check your inbox.");
    }

    private async Task<Result> RegisterAsync(RegisterDto dto, Role role, Action<ApplicationUser> addProfile, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(dto.Email) != null)
            return Result.Conflict("Email is already registered", ErrorCodes.EmailAlreadyRegistered);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var user = new ApplicationUser
        {
            UserName = Guid.NewGuid().ToString(),
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Gender = dto.Gender,
            PhoneNumber = dto.PhoneNumber,
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return Result.Validation(result.ErrorText());

        result = await userManager.AddToRoleAsync(user, role.ToString());
        if (!result.Succeeded)
            return Result.Fail(result.ErrorText());

        addProfile(user);

        if (dto.ProfilePictureFile != null)
        {
            var upload = await mediaService.UploadImageAsync(dto.ProfilePictureFile, cancellationToken);
            if (!upload.IsSuccess)
                return Result.Validation(upload.Message, ErrorCodes.InvalidFile);
            user.ProfilePictureUrl = upload.Data!.FakeName;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await appEmailService.SendConfirmEmailAsync(user, token, cancellationToken);

        return Result.Success("Registration successful. Please confirm your email before logging in.");
    }
}
