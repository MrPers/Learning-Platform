using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Services.Users;

public class UserService(UserManager<ApplicationUser> userManager, IMediaService mediaService, IAppDbContext db) : IUserService
{
    public async Task<Result<List<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users.OrderBy(x => x.Email).Select(Mappings.UserToDto).ToListAsync(cancellationToken);
        return Result<List<UserDto>>.Success(users);
    }

    public async Task<Result<UserDto>> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.Where(x => x.Id == userId).Select(Mappings.UserToDto).FirstOrDefaultAsync(cancellationToken);
        if (user == null)
            return Result<UserDto>.NotFound("User Not Found", ErrorCodes.UserNotFound);

        return Result<UserDto>.Success(user);
    }

    public async Task<Result> LockAccountAsync(UserActionDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(dto.UserId);
        if (user == null)
            return UserNotFound();

        if (await userManager.IsInRoleAsync(user, RolesConstants.Admin))
            return Result.Forbidden("Cannot lock Admin account");

        var result = await userManager.SetLockoutEnabledAsync(user, true);
        if (result.Succeeded)
            result = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!result.Succeeded)
            return Result.Fail(result.ErrorText());

        // A locked user must not keep a working session
        await db.RefreshTokens.Where(x => x.ApplicationUserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        return Result.Success("Account locked Successfully");
    }

    public async Task<Result> UnlockAccountAsync(UserActionDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(dto.UserId);
        if (user == null)
            return UserNotFound();

        var result = await userManager.SetLockoutEndDateAsync(user, null);
        if (result.Succeeded)
            result = await userManager.ResetAccessFailedCountAsync(user);
        if (!result.Succeeded)
            return Result.Fail(result.ErrorText());

        return Result.Success("Account Unlocked Successfully");
    }

    public async Task<Result> AddToRoleAsync(string userId, Role role, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return UserNotFound();

        if (await userManager.IsInRoleAsync(user, role.ToString()))
            return Result.Conflict($"User is already in the role {role}");

        var result = await userManager.AddToRoleAsync(user, role.ToString());
        if (!result.Succeeded)
            return Result.Fail(result.ErrorText());

        return Result.Success($"User added to role {role} successfully");
    }

    public async Task<Result> RemoveFromRoleAsync(string userId, Role role, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return UserNotFound();

        if (role == Role.Admin)
            return Result.Forbidden("Cannot remove Admin role");

        if (!await userManager.IsInRoleAsync(user, role.ToString()))
            return Result.Validation($"User is not in the role {role}");

        var result = await userManager.RemoveFromRoleAsync(user, role.ToString());
        if (!result.Succeeded)
            return Result.Fail(result.ErrorText());

        return Result.Success($"User removed from role {role} successfully");
    }

    public async Task<Result<List<TeacherDto>>> GetAllTeachersAsync(CancellationToken cancellationToken = default)
    {
        var teachersDto = await db.Teachers.OrderBy(x => x.Id).Select(Mappings.TeacherToDto).ToListAsync(cancellationToken);
        return Result<List<TeacherDto>>.Success(teachersDto);
    }

    public async Task<Result<List<StudentDto>>> GetAllStudentsAsync(CancellationToken cancellationToken = default)
    {
        var studentsDto = await db.Students.OrderBy(x => x.Id).Select(Mappings.StudentToDto).ToListAsync(cancellationToken);
        return Result<List<StudentDto>>.Success(studentsDto);
    }

    public async Task<Result> UpdateProfileAsync(string userId, UpdateProfileDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return UserNotFound();

        if (!string.IsNullOrEmpty(dto.FirstName))
            user.FirstName = dto.FirstName;
        if (!string.IsNullOrEmpty(dto.LastName))
            user.LastName = dto.LastName;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return Result.Fail(updateResult.ErrorText());

        return Result.Success("Profile updated successfully");
    }

    public async Task<Result> UpdateProfilePictureAsync(string userId, UpdatePictureProfileDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return UserNotFound();

        var upload = await mediaService.UploadImageAsync(dto.PictureFile, cancellationToken);
        if (!upload.IsSuccess)
            return Result.Validation(upload.Message, ErrorCodes.InvalidFile);

        var oldPicture = user.ProfilePictureUrl;
        user.ProfilePictureUrl = upload.Data!.FakeName;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            mediaService.RemoveImage(upload.Data.FakeName);
            return Result.Fail(updateResult.ErrorText());
        }

        if (!string.IsNullOrEmpty(oldPicture))
            mediaService.RemoveImage(oldPicture);

        return Result.Success("Profile picture updated successfully");
    }

    private static Result UserNotFound() => Result.NotFound("User Not Found", ErrorCodes.UserNotFound);
}
