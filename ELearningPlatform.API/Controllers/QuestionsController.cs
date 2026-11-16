namespace ELearningPlatform.API.Controllers;

/// <summary>Questions include the correct answers, so only admins and teachers can see them.</summary>
[ApiController]
[Route("api/Exam/{examId:int}/[controller]")]
[Authorize(Policy = PoliciesConstants.AdminTeacher)]
public class QuestionsController(IQuestionService questionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(int examId, CancellationToken cancellationToken)
        => (await questionService.GetAllQuestionAsync(examId, cancellationToken)).ToActionResult(this);

    [HttpGet("Pagination")]
    public async Task<IActionResult> GetPage(int examId, int pageNumber = 1, int pageSize = PaginationExtensions.DefaultPageSize, CancellationToken cancellationToken = default)
        => (await questionService.GetAllQuestionWithPaginationAsync(this.CurrentUrl(), examId, pageNumber, pageSize, cancellationToken)).ToActionResult(this);

    [HttpGet("{questionNumber}")]
    public async Task<IActionResult> GetByNumber(int examId, byte questionNumber, CancellationToken cancellationToken)
        => (await questionService.GetQuestionByIdAsync(examId, questionNumber, cancellationToken)).ToActionResult(this);

    [HttpPost("MCQ_Question")]
    public async Task<IActionResult> CreateMcq(int examId, CreateMcqQuestionDto dto, CancellationToken cancellationToken)
        => Created(await questionService.CreateMcqQuestionAsync(examId, dto, cancellationToken), examId);

    [HttpPost("TF_Question")]
    public async Task<IActionResult> CreateTrueFalse(int examId, CreateTFQuestionDto dto, CancellationToken cancellationToken)
        => Created(await questionService.CreateTFQuestionAsync(examId, dto, cancellationToken), examId);

    [HttpPut("{questionNumber}")]
    public async Task<IActionResult> UpdateMark(int examId, byte questionNumber, UpdateQuestionDegreeDto dto, CancellationToken cancellationToken)
        => (await questionService.UpdateQuestionDegreeAsync(examId, questionNumber, dto, cancellationToken)).ToActionResult(this);

    [HttpDelete("{questionNumber}")]
    public async Task<IActionResult> Delete(int examId, byte questionNumber, CancellationToken cancellationToken)
        => (await questionService.DeleteQuestionAsync(examId, questionNumber, cancellationToken)).ToActionResult(this);

    private IActionResult Created(Result<QuestionDto> result, int examId)
        => result.IsSuccess
            ? CreatedAtAction(nameof(GetByNumber), new { examId, questionNumber = result.Data!.QuestionNumber }, result.Data)
            : result.ToProblem(this);
}
