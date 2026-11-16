namespace ELearningPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
// Policies are per action: a class-level AdminOnly would be ANDed with the profile actions' policy
public class UsersController(IUserService userService) : ControllerBase
{
    [Authorize(Policy = PoliciesConstants.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => (await userService.GetAllUsersAsync(cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminOnly)]
    [HttpGet("Teachers")]
    public async Task<IActionResult> GetTeachers(CancellationToken cancellationToken)
        => (await userService.GetAllTeachersAsync(cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminOnly)]
    [HttpGet("Students")]
    public async Task<IActionResult> GetStudents(CancellationToken cancellationToken)
        => (await userService.GetAllStudentsAsync(cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacherStudent)]
    [HttpGet("Profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        => (await userService.GetUserByIdAsync(User.GetUserId(), cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacherStudent)]
    [HttpPut("Profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto, CancellationToken cancellationToken)
        => (await userService.UpdateProfileAsync(User.GetUserId(), dto, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacherStudent)]
    [HttpPatch("Profile/Picture")]
    public async Task<IActionResult> UpdateProfilePicture([FromForm] UpdatePictureProfileDto dto, CancellationToken cancellationToken)
        => (await userService.UpdateProfilePictureAsync(User.GetUserId(), dto, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminOnly)]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
        => (await userService.GetUserByIdAsync(id, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminOnly)]
    [HttpPost("add-role")]
    public async Task<IActionResult> AddRole(UserRoleDto dto, CancellationToken cancellationToken)
        => (await userService.AddToRoleAsync(dto.UserId, dto.Role, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminOnly)]
    [HttpDelete("remove-role")]
    public async Task<IActionResult> RemoveRole(UserRoleDto dto, CancellationToken cancellationToken)
        => (await userService.RemoveFromRoleAsync(dto.UserId, dto.Role, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminOnly)]
    [HttpPatch("{id}/lock")]
    public async Task<IActionResult> Lock(string id, CancellationToken cancellationToken)
        => (await userService.LockAccountAsync(new UserActionDto { UserId = id }, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminOnly)]
    [HttpPatch("{id}/unlock")]
    public async Task<IActionResult> Unlock(string id, CancellationToken cancellationToken)
        => (await userService.UnlockAccountAsync(new UserActionDto { UserId = id }, cancellationToken)).ToActionResult(this);
}
