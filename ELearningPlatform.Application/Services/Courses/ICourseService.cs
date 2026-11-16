namespace ELearningPlatform.Application.Services.Courses;

public interface ICourseService
{
    Task<Result<List<CourseDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<CourseDto>>> GetAllWithPaginationsAsync(int pageNumber, int pageSize, string baseUrl, CancellationToken cancellationToken = default);
    Task<Result<CourseDto>> GetByIdAsync(int courseId, CancellationToken cancellationToken = default);
    Task<Result<CourseDto>> AddAsync(string userId, CreateCourseDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int courseId, UpdateCourseDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdatePriceAsync(int courseId, UpdateCoursePriceDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int courseId, CancellationToken cancellationToken = default);
    Task<Result<List<CourseDto>>> SearchByNameAsync(string courseName, CancellationToken cancellationToken = default);
    Task<Result<List<CourseDto>>> SearchBySpecializationAsync(Specialization specialization, CancellationToken cancellationToken = default);
    Task<Result> AddCommentToCourseAsync(string userId, int courseId, AddCommentDto dto, CancellationToken cancellationToken = default);
    Task<Result> RateCourseAsync(string userId, int courseId, CourseReactionDto dto, CancellationToken cancellationToken = default);
    Task<Result<CourseDto>> GetTopRatedCourseAsync(CancellationToken cancellationToken = default);
    Task<Result<List<CommentDto>>> GetAllCommentsAsync(int courseId, CancellationToken cancellationToken = default);
}
