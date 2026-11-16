namespace ELearningPlatform.Application.Services.Users;

public interface IUserService
{
    Task<Result<List<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<Result<List<TeacherDto>>> GetAllTeachersAsync(CancellationToken cancellationToken = default);
    Task<Result<List<StudentDto>>> GetAllStudentsAsync(CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result> LockAccountAsync(UserActionDto dto, CancellationToken cancellationToken = default);
    Task<Result> UnlockAccountAsync(UserActionDto dto, CancellationToken cancellationToken = default);
    Task<Result> AddToRoleAsync(string userId, Role role, CancellationToken cancellationToken = default);
    Task<Result> RemoveFromRoleAsync(string userId, Role role, CancellationToken cancellationToken = default);
    Task<Result> UpdateProfileAsync(string userId, UpdateProfileDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateProfilePictureAsync(string userId, UpdatePictureProfileDto dto, CancellationToken cancellationToken = default);
}
