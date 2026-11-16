namespace ELearningPlatform.Application.Services.Questions;

public interface IQuestionService
{
    Task<Result<List<QuestionDto>>> GetAllQuestionAsync(int examId, CancellationToken cancellationToken = default);
    Task<Result<QuestionDto>> GetQuestionByIdAsync(int examId, byte questionNumber, CancellationToken cancellationToken = default);
    Task<Result<QuestionDto>> CreateMcqQuestionAsync(int examId, CreateMcqQuestionDto dto, CancellationToken cancellationToken = default);
    Task<Result<QuestionDto>> CreateTFQuestionAsync(int examId, CreateTFQuestionDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateQuestionDegreeAsync(int examId, byte questionNumber, UpdateQuestionDegreeDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteQuestionAsync(int examId, byte questionNumber, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<QuestionDto>>> GetAllQuestionWithPaginationAsync(string baseUrl, int examId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
