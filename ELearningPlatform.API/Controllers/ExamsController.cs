namespace ELearningPlatform.API.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/[controller]")]
[Authorize(Policy = PoliciesConstants.AdminTeacherStudent)]
public class ExamsController(IExamService examService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(int courseId, CancellationToken cancellationToken)
        => (await examService.GetAllAsync(courseId, cancellationToken)).ToActionResult(this);

    [HttpGet("{examId:int}")]
    public async Task<IActionResult> GetById(int courseId, int examId, CancellationToken cancellationToken)
        => (await examService.GetByIdAsync(courseId, examId, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPost]
    public async Task<IActionResult> Create(int courseId, CreateExamDto dto, CancellationToken cancellationToken)
    {
        var result = await examService.CreateAsync(User.GetUserId(), courseId, dto, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { courseId, examId = result.Data!.Id }, result.Data)
            : result.ToProblem(this);
    }

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPut("{examId:int}")]
    public async Task<IActionResult> Update(int courseId, int examId, UpdateExamDto dto, CancellationToken cancellationToken)
        => (await examService.UpdateAsync(courseId, examId, dto, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPatch("{examId:int}")]
    public async Task<IActionResult> UpdateTime(int courseId, int examId, UpdateExamTimeDto dto, CancellationToken cancellationToken)
        => (await examService.UpdateExamTimeAsync(courseId, examId, dto, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpDelete("{examId:int}")]
    public async Task<IActionResult> Delete(int courseId, int examId, CancellationToken cancellationToken)
        => (await examService.DeleteAsync(courseId, examId, cancellationToken)).ToActionResult(this);

    [Authorize(Roles = RolesConstants.Student)]
    [HttpGet("{examId:int}/Paper")]
    public async Task<IActionResult> GetPaper(int courseId, int examId, CancellationToken cancellationToken)
        => (await examService.GetPaperAsync(User.GetUserId(), courseId, examId, cancellationToken)).ToActionResult(this);

    [Authorize(Roles = RolesConstants.Student)]
    [HttpPost("{examId:int}/Submit")]
    public async Task<IActionResult> Submit(int courseId, int examId, List<SubmitExamDto> answers, CancellationToken cancellationToken)
        => (await examService.SubmitExamAsync(User.GetUserId(), courseId, examId, answers, cancellationToken)).ToActionResult(this);

    [Authorize(Roles = RolesConstants.Student)]
    [HttpGet("{examId:int}/Result")]
    public async Task<IActionResult> GetResult(int courseId, int examId, CancellationToken cancellationToken)
        => (await examService.GetExamResultAsync(User.GetUserId(), courseId, examId, cancellationToken)).ToActionResult(this);
}
