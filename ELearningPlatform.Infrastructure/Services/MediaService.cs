using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Infrastructure.Services;

public partial class MediaService(IOptions<UploadsSettings> options, IWebHostEnvironment webHost, ILogger<MediaService> logger) : IMediaService
{
    private const int StreamBufferSize = 81920;
    private readonly UploadsSettings _uploadsSettings = options.Value;

    private string UploadsRoot => Path.Combine(
        webHost.WebRootPath ?? Path.Combine(webHost.ContentRootPath, "wwwroot"),
        FolderConstants.UploadsFolder);

    public Task<Result<MediaDto>> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default)
        => UploadAsync(file, MediaType.Image, _uploadsSettings.ImageSizeInMB, _uploadsSettings.ImageExtentionAllowed, FolderConstants.ImagesFolder, cancellationToken);

    public Task<Result<MediaDto>> UploadVideoAsync(IFormFile file, CancellationToken cancellationToken = default)
        => UploadAsync(file, MediaType.Video, _uploadsSettings.VideoSizeInMB, _uploadsSettings.VideoExtentionAllowed, FolderConstants.VideosFolder, cancellationToken);

    public FileDownloadDto? OpenRead(MediaDto mediaDto, string folder)
    {
        if (!IsPlainFileName(mediaDto.FakeName))
            return null;

        var fullPath = Path.Combine(UploadsRoot, folder, mediaDto.FakeName);
        if (!File.Exists(fullPath))
            return null;

        // Streamed to the client (with range support) instead of loading the whole video into memory
        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize, useAsync: true);
        return new FileDownloadDto
        {
            Content = stream,
            ContentType = mediaDto.ContentType,
            DownloadName = mediaDto.OriginalName,
        };
    }

    public Result RemoveImage(string imageName) => Remove(imageName, FolderConstants.ImagesFolder, MediaType.Image);

    public Result RemoveVideo(string videoName) => Remove(videoName, FolderConstants.VideosFolder, MediaType.Video);

    private Result Remove(string name, string folderName, MediaType mediaType)
    {
        if (!IsPlainFileName(name))
            return Result.Validation($"Invalid {mediaType} name");

        var path = Path.Combine(UploadsRoot, folderName, name);
        if (!File.Exists(path))
            return Result.NotFound($"{mediaType} not Exist");

        File.Delete(path);
        return Result.Success("Deleted Successfully");
    }

    private async Task<Result<MediaDto>> UploadAsync(IFormFile? file, MediaType mediaType, int sizeInMB,
        List<string> allowedExtensions, string folderName, CancellationToken cancellationToken)
    {
        if (file == null)
            return Result<MediaDto>.Validation($"{mediaType} Can't be null", ErrorCodes.InvalidFile);

        if (!FileValidationHelper.CheckSize(file, sizeInMB))
            return Result<MediaDto>.Validation($"{mediaType} size must not exceed {sizeInMB} MB.", ErrorCodes.InvalidFile);

        if (!FileValidationHelper.CheckExtension(file, allowedExtensions))
            return Result<MediaDto>.Validation($"invalid Format. {mediaType} must be {string.Join(',', allowedExtensions)} format.", ErrorCodes.InvalidFile);

        if (!FileValidationHelper.CheckSignature(file))
        {
            LogSignatureMismatch(mediaType, file.FileName);
            return Result<MediaDto>.Validation($"{mediaType} content does not match its file type.", ErrorCodes.InvalidFile);
        }

        var folderPath = Path.Combine(UploadsRoot, folderName);
        Directory.CreateDirectory(folderPath);

        // The stored name is ours; the client's file name is kept only as metadata
        var newName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using (var fileStream = new FileStream(Path.Combine(folderPath, newName), FileMode.CreateNew, FileAccess.Write, FileShare.None, StreamBufferSize, useAsync: true))
            await file.CopyToAsync(fileStream, cancellationToken);

        return Result<MediaDto>.Success(new MediaDto
        {
            OriginalName = Path.GetFileName(file.FileName),
            FakeName = newName,
            ContentType = file.ContentType,
            MediaType = mediaType,
        });
    }

    private static bool IsPlainFileName(string name)
        => !string.IsNullOrWhiteSpace(name) && Path.GetFileName(name) == name && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected {MediaType} upload \"{FileName}\": content does not match its extension")]
    private partial void LogSignatureMismatch(MediaType mediaType, string fileName);
}
