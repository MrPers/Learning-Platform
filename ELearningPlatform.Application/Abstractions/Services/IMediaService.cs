using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.Abstractions.Services;

public interface IMediaService
{
    Task<Result<MediaDto>> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<Result<MediaDto>> UploadVideoAsync(IFormFile file, CancellationToken cancellationToken = default);

    /// <summary>Opens the stored file for streaming; the caller owns (and must dispose) the stream.</summary>
    FileDownloadDto? OpenRead(MediaDto mediaDto, string folder);

    Result RemoveImage(string imageName);
    Result RemoveVideo(string videoName);
}
