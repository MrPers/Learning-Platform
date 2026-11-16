namespace ELearningPlatform.Application.Services.Videos;

public interface IVideoService
{
    Task<Result<List<VideoDto>>> GetAllAsync(int courseId, CancellationToken cancellationToken = default);
    Task<Result<PaginatedResult<VideoDto>>> GetAllWithPaginationAsync(int courseId, int pageNumber, int pageSize, string baseUrl, CancellationToken cancellationToken = default);
    Task<Result<VideoDto>> GetByIdAsync(int courseId, int videoId, CancellationToken cancellationToken = default);
    Task<Result<List<VideoDto>>> SearchByNameAsync(int courseId, string videoName, CancellationToken cancellationToken = default);
    Task<Result<VideoDto>> AddVideoAsync(int courseId, CreateVideoDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int courseId, int videoId, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int courseId, int videoId, UpdateVideoDto dto, CancellationToken cancellationToken = default);
    Task<Result> UpdateVideoAsync(int courseId, int videoId, UpdateVideoFileDto dto, CancellationToken cancellationToken = default);
    Task<Result<FileDownloadDto>> DownloadVideoAsync(int courseId, int videoId, CancellationToken cancellationToken = default);
    Task<Result> AddCommentToVideoAsync(string userId, int courseId, int videoId, AddCommentDto dto, CancellationToken cancellationToken = default);
    Task<Result> ReactOnVideoAsync(string userId, int courseId, int videoId, bool isLike, CancellationToken cancellationToken = default);
    Task<Result<List<CommentDto>>> GetAllCommentsAsync(int courseId, int videoId, CancellationToken cancellationToken = default);
    Task<Result<List<VideoDto>>> GetTrendingVideosAsync(int courseId, CancellationToken cancellationToken = default);
}
