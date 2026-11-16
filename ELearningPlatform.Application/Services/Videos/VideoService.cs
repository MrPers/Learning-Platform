using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Services.Videos;

public class VideoService(IAppDbContext db, IMediaService mediaService, TimeProvider timeProvider) : IVideoService
{
    public const int TrendingCount = 10;

    public async Task<Result> AddCommentToVideoAsync(string userId, int courseId, int videoId, AddCommentDto dto, CancellationToken cancellationToken = default)
    {
        if (!await VideoExistsAsync(courseId, videoId, cancellationToken))
            return VideoNotFound();

        db.VideoComments.Add(new VideoComment
        {
            Comment = dto.Comment,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            UserId = userId,
            VideoId = videoId,
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Comment Added Successfully");
    }

    public async Task<Result<VideoDto>> AddVideoAsync(int courseId, CreateVideoDto dto, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return Result<VideoDto>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        var upload = await mediaService.UploadVideoAsync(dto.VideoFile, cancellationToken);
        if (!upload.IsSuccess)
            return Result<VideoDto>.Validation(upload.Message, ErrorCodes.InvalidFile);

        var video = dto.ToEntity();
        video.CourseId = courseId;
        video.VideoUrl = upload.Data!.FakeName;
        video.ContentType = upload.Data.ContentType;
        video.UploadedAt = timeProvider.GetUtcNow().UtcDateTime;
        db.Videos.Add(video);
        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(courseId, video.Id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int courseId, int videoId, CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(courseId, videoId, cancellationToken);
        if (video == null)
            return VideoNotFound();

        db.Videos.Remove(video);
        await db.SaveChangesAsync(cancellationToken);
        // Remove the file only after the row is gone, so a failed save never leaves a row without a file
        mediaService.RemoveVideo(video.VideoUrl);
        return Result.Success("Deleted Successfully");
    }

    public async Task<Result<FileDownloadDto>> DownloadVideoAsync(int courseId, int videoId, CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(courseId, videoId, cancellationToken);
        if (video == null)
            return Result<FileDownloadDto>.NotFound("Video Not Found", ErrorCodes.VideoNotFound);

        var mediaDto = new MediaDto { FakeName = video.VideoUrl, ContentType = video.ContentType, OriginalName = video.OriginalName };
        var file = mediaService.OpenRead(mediaDto, FolderConstants.VideosFolder);
        if (file == null)
            return Result<FileDownloadDto>.NotFound("Video file is missing in storage", ErrorCodes.VideoNotFound);

        return Result<FileDownloadDto>.Success(file);
    }

    public async Task<Result<List<VideoDto>>> GetAllAsync(int courseId, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return Result<List<VideoDto>>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        var videosDto = await db.Videos.Where(v => v.CourseId == courseId).OrderBy(v => v.Id)
            .Select(Mappings.VideoToDto).ToListAsync(cancellationToken);
        return Result<List<VideoDto>>.Success(videosDto);
    }

    public async Task<Result<List<CommentDto>>> GetAllCommentsAsync(int courseId, int videoId, CancellationToken cancellationToken = default)
    {
        if (!await VideoExistsAsync(courseId, videoId, cancellationToken))
            return Result<List<CommentDto>>.NotFound("Video Not Found", ErrorCodes.VideoNotFound);

        var commentsDto = await db.VideoComments.Where(x => x.VideoId == videoId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(Mappings.VideoCommentToDto).ToListAsync(cancellationToken);
        return Result<List<CommentDto>>.Success(commentsDto);
    }

    public async Task<Result<PaginatedResult<VideoDto>>> GetAllWithPaginationAsync(int courseId, int pageNumber, int pageSize, string baseUrl, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return Result<PaginatedResult<VideoDto>>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        var page = await db.Videos.Where(x => x.CourseId == courseId).OrderBy(x => x.Id)
            .Select(Mappings.VideoToDto)
            .ToPaginatedResultAsync(pageNumber, pageSize, baseUrl, cancellationToken);
        return Result<PaginatedResult<VideoDto>>.Success(page);
    }

    public async Task<Result<VideoDto>> GetByIdAsync(int courseId, int videoId, CancellationToken cancellationToken = default)
    {
        var videoDto = await db.Videos.Where(x => x.Id == videoId && x.CourseId == courseId)
            .Select(Mappings.VideoToDto).FirstOrDefaultAsync(cancellationToken);
        if (videoDto == null)
            return Result<VideoDto>.NotFound("Video Not Found", ErrorCodes.VideoNotFound);

        return Result<VideoDto>.Success(videoDto);
    }

    public async Task<Result<List<VideoDto>>> GetTrendingVideosAsync(int courseId, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return Result<List<VideoDto>>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        var videosDto = await db.Videos
            .Where(v => v.CourseId == courseId && v.VideoReactions.Any())
            .OrderByDescending(v => v.VideoReactions.Count(r => r.IsLike) - v.VideoReactions.Count(r => !r.IsLike))
            .ThenByDescending(v => v.VideoReactions.Count(r => r.IsLike))
            .ThenBy(v => v.Id)
            .Take(TrendingCount)
            .Select(Mappings.VideoToDto)
            .ToListAsync(cancellationToken);
        return Result<List<VideoDto>>.Success(videosDto);
    }

    public async Task<Result> ReactOnVideoAsync(string userId, int courseId, int videoId, bool isLike, CancellationToken cancellationToken = default)
    {
        if (!await VideoExistsAsync(courseId, videoId, cancellationToken))
            return VideoNotFound();

        // One reaction per user: reacting again replaces the previous one
        var reaction = await db.VideoReactions.FirstOrDefaultAsync(x => x.VideoId == videoId && x.UserId == userId, cancellationToken);
        if (reaction == null)
        {
            reaction = new VideoReaction { UserId = userId, VideoId = videoId };
            db.VideoReactions.Add(reaction);
        }
        reaction.IsLike = isLike;
        reaction.ReactedAt = timeProvider.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Reacted Successfully");
    }

    public async Task<Result<List<VideoDto>>> SearchByNameAsync(int courseId, string videoName, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return Result<List<VideoDto>>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        var candidates = await db.Videos.Where(x => x.CourseId == courseId)
            .Select(x => new { x.Id, x.Title, x.OriginalName })
            .ToListAsync(cancellationToken);
        var rankedIds = FuzzySearch.RankIds(candidates.Select(c => (c.Id, new[] { c.Title, c.OriginalName })), videoName);
        if (rankedIds.Count == 0)
            return Result<List<VideoDto>>.Success([]);

        var videos = await db.Videos.Where(x => rankedIds.Contains(x.Id))
            .Select(Mappings.VideoToDto).ToListAsync(cancellationToken);
        return Result<List<VideoDto>>.Success(FuzzySearch.OrderByRank(videos, v => v.Id, rankedIds));
    }

    public async Task<Result> UpdateAsync(int courseId, int videoId, UpdateVideoDto dto, CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(courseId, videoId, cancellationToken);
        if (video == null)
            return VideoNotFound();

        if (!string.IsNullOrWhiteSpace(dto.Title))
            video.Title = dto.Title;

        if (!string.IsNullOrWhiteSpace(dto.Description))
            video.Description = dto.Description;

        if (!string.IsNullOrWhiteSpace(dto.OriginalName))
            video.OriginalName = dto.OriginalName;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Updated Successfully");
    }

    public async Task<Result> UpdateVideoAsync(int courseId, int videoId, UpdateVideoFileDto dto, CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(courseId, videoId, cancellationToken);
        if (video == null)
            return VideoNotFound();

        // Upload first so a failed upload doesn't leave the video without a file
        var upload = await mediaService.UploadVideoAsync(dto.VideoFile, cancellationToken);
        if (!upload.IsSuccess)
            return Result.Validation(upload.Message, ErrorCodes.InvalidFile);

        var oldFile = video.VideoUrl;
        video.VideoUrl = upload.Data!.FakeName;
        video.ContentType = upload.Data.ContentType;

        await db.SaveChangesAsync(cancellationToken);
        mediaService.RemoveVideo(oldFile);
        return Result.Success("Updated Successfully");
    }

    private Task<Video?> FindVideoAsync(int courseId, int videoId, CancellationToken cancellationToken)
        => db.Videos.FirstOrDefaultAsync(x => x.Id == videoId && x.CourseId == courseId, cancellationToken);

    private Task<bool> VideoExistsAsync(int courseId, int videoId, CancellationToken cancellationToken)
        => db.Videos.AnyAsync(x => x.Id == videoId && x.CourseId == courseId, cancellationToken);

    private static Result VideoNotFound() => Result.NotFound("Video Not Found", ErrorCodes.VideoNotFound);
}
