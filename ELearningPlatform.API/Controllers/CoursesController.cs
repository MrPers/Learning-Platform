namespace ELearningPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = PoliciesConstants.AdminTeacherStudent)]
public class CoursesController(ICourseService courseService, IPurchaseService purchaseService) : ControllerBase
{
    /// <summary>
    /// Buys the course with the wallet balance (platform fee to the platform, the rest to the teacher) and
    /// enrolls the student. Requires an Idempotency-Key header.
    /// </summary>
    [Authorize(Policy = PoliciesConstants.StudentOnly)]
    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [Idempotent]
    [HttpPost("{id:int}/purchase")]
    public async Task<IActionResult> Purchase(int id, CancellationToken cancellationToken)
    {
        var result = await purchaseService.PurchaseAsync(User.GetUserId(), id, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Data) : result.ToProblem(this);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => (await courseService.GetAllAsync(cancellationToken)).ToActionResult(this);

    [HttpGet("CoursesPagination")]
    public async Task<IActionResult> GetPage(int pageNumber = 1, int pageSize = PaginationExtensions.DefaultPageSize, CancellationToken cancellationToken = default)
        => (await courseService.GetAllWithPaginationsAsync(pageNumber, pageSize, this.CurrentUrl(), cancellationToken)).ToActionResult(this);

    [HttpGet("TopRated")]
    public async Task<IActionResult> GetTopRated(CancellationToken cancellationToken)
        => (await courseService.GetTopRatedCourseAsync(cancellationToken)).ToActionResult(this);

    [HttpGet("specialization/{specialization}")]
    public async Task<IActionResult> GetBySpecialization(Specialization specialization, CancellationToken cancellationToken)
        => (await courseService.SearchBySpecializationAsync(specialization, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => (await courseService.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

    [HttpGet("{courseName}")]
    public async Task<IActionResult> SearchByName(string courseName, CancellationToken cancellationToken)
        => (await courseService.SearchByNameAsync(courseName, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCourseDto dto, CancellationToken cancellationToken)
    {
        var result = await courseService.AddAsync(User.GetUserId(), dto, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data)
            : result.ToProblem(this);
    }

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateCourseDto dto, CancellationToken cancellationToken)
        => (await courseService.UpdateAsync(id, dto, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdatePrice(int id, UpdateCoursePriceDto dto, CancellationToken cancellationToken)
        => (await courseService.UpdatePriceAsync(id, dto, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        => (await courseService.DeleteAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost("{id:int}/Rate")]
    public async Task<IActionResult> Rate(int id, CourseReactionDto dto, CancellationToken cancellationToken)
        => (await courseService.RateCourseAsync(User.GetUserId(), id, dto, cancellationToken)).ToActionResult(this);

    [HttpPost("{id:int}/Comment")]
    public async Task<IActionResult> Comment(int id, AddCommentDto dto, CancellationToken cancellationToken)
        => (await courseService.AddCommentToCourseAsync(User.GetUserId(), id, dto, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:int}/Comments")]
    public async Task<IActionResult> GetComments(int id, CancellationToken cancellationToken)
        => (await courseService.GetAllCommentsAsync(id, cancellationToken)).ToActionResult(this);
}
