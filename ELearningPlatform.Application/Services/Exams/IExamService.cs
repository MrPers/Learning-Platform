namespace ELearningPlatform.Application.Services.Exams;

public interface IExamService
{
    Task<Result<List<ExamDto>>> GetAllAsync(int courseId, CancellationToken cancellationToken = default);
    Task<Result<ExamDto>> GetByIdAsync(int courseId, int examId, CancellationToken cancellationToken = default);
    Task<Result<ExamDto>> CreateAsync(string userId, int courseId, CreateExamDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int courseId, int examId, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int courseId, int examId, UpdateExamDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateExamTimeAsync(int courseId, int examId, UpdateExamTimeDto dto, CancellationToken cancellationToken = default);
    Task<Result<ExamPaperDto>> GetPaperAsync(string userId, int courseId, int examId, CancellationToken cancellationToken = default);
    Task<Result> SubmitExamAsync(string userId, int courseId, int examId, List<SubmitExamDto> dto, CancellationToken cancellationToken = default);
    Task<Result<ResultExamDto>> GetExamResultAsync(string userId, int courseId, int examId, CancellationToken cancellationToken = default);
}
