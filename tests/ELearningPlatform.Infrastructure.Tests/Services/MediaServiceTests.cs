using ELearningPlatform.Application.Common.Results;
using Microsoft.AspNetCore.Hosting;

namespace ELearningPlatform.Infrastructure.Tests.Services;

public sealed class MediaServiceTests : IDisposable
{
    private readonly string _root = TestFiles.NewTempDirectory();
    private readonly IWebHostEnvironment _env = Substitute.For<IWebHostEnvironment>();

    public MediaServiceTests()
    {
        _env.WebRootPath.Returns(Path.Combine(_root, "wwwroot"));
        _env.ContentRootPath.Returns(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private MediaService CreateService() => new(Options.Create(TestSettings.Uploads()), _env, NullLogger<MediaService>.Instance);

    private string Folder(string folder) => Path.Combine(_root, "wwwroot", FolderConstants.UploadsFolder, folder);

    private static Microsoft.AspNetCore.Http.IFormFile Jpeg(string name = "photo.jpg", int size = 64)
        => TestFiles.FormFile(name, TestFiles.WithHeader(TestFiles.JpegHeader, size), "image/jpeg");

    private static Microsoft.AspNetCore.Http.IFormFile Mp4(string name = "lesson.mp4", int size = 64)
        => TestFiles.FormFile(name, TestFiles.WithHeader(TestFiles.Mp4Header, size), "video/mp4");

    [Fact]
    public async Task UploadImage_WritesFileWithGeneratedName()
    {
        var result = await CreateService().UploadImageAsync(Jpeg());

        Assert.True(result.IsSuccess, result.Message);
        var media = result.Data!;
        Assert.Matches("^[0-9a-f]{32}\\.jpg$", media.FakeName);
        Assert.Equal("photo.jpg", media.OriginalName);
        Assert.Equal("image/jpeg", media.ContentType);
        Assert.Equal(MediaType.Image, media.MediaType);
        Assert.True(File.Exists(Path.Combine(Folder(FolderConstants.ImagesFolder), media.FakeName)));
    }

    [Fact]
    public async Task UploadImage_StoresExactBytes()
    {
        var bytes = TestFiles.WithHeader(TestFiles.PngHeader, 500);
        var result = await CreateService().UploadImageAsync(TestFiles.FormFile("a.png", bytes, "image/png"));

        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(Folder(FolderConstants.ImagesFolder), result.Data!.FakeName)));
    }

    [Theory]
    [InlineData("PHOTO.JPG", ".jpg")]
    [InlineData("Photo.JpEg", ".jpeg")]
    public async Task UploadImage_LowercasesExtension(string fileName, string extension)
    {
        var result = await CreateService().UploadImageAsync(Jpeg(fileName));

        Assert.EndsWith(extension, result.Data!.FakeName);
    }

    [Fact]
    public async Task UploadImage_OriginalNameDropsClientDirectories()
    {
        var result = await CreateService().UploadImageAsync(Jpeg("../../evil/photo.jpg"));

        Assert.True(result.IsSuccess);
        Assert.Equal("photo.jpg", result.Data!.OriginalName);
    }

    [Fact]
    public async Task UploadImage_TwoUploadsGetDifferentNames()
    {
        var service = CreateService();
        var a = await service.UploadImageAsync(Jpeg());
        var b = await service.UploadImageAsync(Jpeg());

        Assert.NotEqual(a.Data!.FakeName, b.Data!.FakeName);
    }

    [Fact]
    public async Task UploadVideo_WritesIntoVideosFolder()
    {
        var result = await CreateService().UploadVideoAsync(Mp4());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(MediaType.Video, result.Data!.MediaType);
        Assert.True(File.Exists(Path.Combine(Folder(FolderConstants.VideosFolder), result.Data.FakeName)));
    }

    [Fact]
    public async Task UploadVideo_Webm_Accepted()
    {
        var result = await CreateService().UploadVideoAsync(TestFiles.FormFile("clip.webm", TestFiles.WithHeader(TestFiles.WebmHeader), "video/webm"));
        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task Upload_WebRootMissing_FallsBackToContentRootWwwroot()
    {
        _env.WebRootPath.Returns((string?)null);

        var result = await CreateService().UploadImageAsync(Jpeg());

        Assert.True(File.Exists(Path.Combine(_root, "wwwroot", FolderConstants.UploadsFolder, FolderConstants.ImagesFolder, result.Data!.FakeName)));
    }

    [Fact]
    public async Task Upload_Null_IsValidationError()
    {
        var result = await CreateService().UploadImageAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(ErrorCodes.InvalidFile, result.Error.Code);
    }

    [Fact]
    public async Task Upload_TooLarge_Rejected()
    {
        var result = await CreateService().UploadImageAsync(Jpeg(size: 1024 * 1024 + 1));

        Assert.False(result.IsSuccess);
        Assert.Contains("1 MB", result.Message);
    }

    [Fact]
    public async Task Upload_ExactlyAtLimit_Accepted()
    {
        var result = await CreateService().UploadImageAsync(Jpeg(size: 1024 * 1024));
        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task Upload_Empty_Rejected()
    {
        var result = await CreateService().UploadImageAsync(TestFiles.FormFile("a.jpg", [], "image/jpeg"));
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("photo.gif")]
    [InlineData("photo.exe")]
    [InlineData("photo")]
    [InlineData("photo.jpg.exe")]
    public async Task UploadImage_DisallowedExtension_Rejected(string fileName)
    {
        var result = await CreateService().UploadImageAsync(Jpeg(fileName));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidFile, result.Error.Code);
    }

    [Fact]
    public async Task UploadVideo_ImageExtension_Rejected()
    {
        var result = await CreateService().UploadVideoAsync(Jpeg());
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task UploadImage_ContentNotMatchingExtension_Rejected()
    {
        // An executable renamed to .jpg
        var result = await CreateService().UploadImageAsync(TestFiles.FormFile("photo.jpg", TestFiles.WithHeader("MZ"u8.ToArray()), "image/jpeg"));

        Assert.False(result.IsSuccess);
        Assert.Contains("does not match", result.Message);
    }

    [Fact]
    public async Task UploadImage_PngBytesWithJpgName_Rejected()
    {
        var result = await CreateService().UploadImageAsync(TestFiles.FormFile("photo.jpg", TestFiles.WithHeader(TestFiles.PngHeader), "image/jpeg"));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task RejectedUpload_WritesNothing()
    {
        await CreateService().UploadImageAsync(TestFiles.FormFile("photo.jpg", TestFiles.WithHeader("MZ"u8.ToArray()), "image/jpeg"));

        var folder = Folder(FolderConstants.ImagesFolder);
        Assert.True(!Directory.Exists(folder) || Directory.GetFiles(folder).Length == 0);
    }

    [Fact]
    public async Task Upload_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().UploadImageAsync(Jpeg(), cts.Token));
    }

    [Fact]
    public async Task OpenRead_ReturnsStreamOfStoredFile()
    {
        var service = CreateService();
        var uploaded = (await service.UploadVideoAsync(Mp4())).Data!;

        var download = service.OpenRead(new MediaDto { FakeName = uploaded.FakeName, ContentType = "video/mp4", OriginalName = "lesson.mp4" }, FolderConstants.VideosFolder);

        Assert.NotNull(download);
        await using (download.Content)
        {
            using var ms = new MemoryStream();
            await download.Content.CopyToAsync(ms);
            Assert.Equal(TestFiles.WithHeader(TestFiles.Mp4Header), ms.ToArray());
        }
        Assert.Equal("video/mp4", download.ContentType);
        Assert.Equal("lesson.mp4", download.DownloadName);
    }

    [Fact]
    public void OpenRead_StreamIsReadOnlyAndSeekable()
    {
        var path = Folder(FolderConstants.VideosFolder);
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "v.mp4"), [1, 2, 3]);

        var download = CreateService().OpenRead(new MediaDto { FakeName = "v.mp4" }, FolderConstants.VideosFolder)!;
        using (download.Content)
        {
            Assert.True(download.Content.CanSeek); // needed for range requests
            Assert.False(download.Content.CanWrite);
        }
    }

    [Fact]
    public void OpenRead_MissingFile_ReturnsNull()
        => Assert.Null(CreateService().OpenRead(new MediaDto { FakeName = "nope.mp4" }, FolderConstants.VideosFolder));

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("a/b.mp4")]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenRead_NonPlainFileName_ReturnsNull(string name)
    {
        // A file that a traversal would reach must still not be served
        File.WriteAllText(Path.Combine(_root, "wwwroot", "secret.txt").EnsureDirectory(), "secret");

        Assert.Null(CreateService().OpenRead(new MediaDto { FakeName = name }, FolderConstants.VideosFolder));
    }

    [Fact]
    public async Task RemoveImage_DeletesFile()
    {
        var service = CreateService();
        var uploaded = (await service.UploadImageAsync(Jpeg())).Data!;

        var result = service.RemoveImage(uploaded.FakeName);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(Path.Combine(Folder(FolderConstants.ImagesFolder), uploaded.FakeName)));
    }

    [Fact]
    public async Task RemoveVideo_DeletesFile()
    {
        var service = CreateService();
        var uploaded = (await service.UploadVideoAsync(Mp4())).Data!;

        Assert.True(service.RemoveVideo(uploaded.FakeName).IsSuccess);
        Assert.False(File.Exists(Path.Combine(Folder(FolderConstants.VideosFolder), uploaded.FakeName)));
    }

    [Fact]
    public void Remove_Missing_IsNotFound()
    {
        var result = CreateService().RemoveImage("missing.jpg");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task RemoveImage_DoesNotTouchVideosFolder()
    {
        var service = CreateService();
        var video = (await service.UploadVideoAsync(Mp4())).Data!;

        Assert.False(service.RemoveImage(video.FakeName).IsSuccess);
        Assert.True(File.Exists(Path.Combine(Folder(FolderConstants.VideosFolder), video.FakeName)));
    }

    [Theory]
    [InlineData("../../important.txt")]
    [InlineData("sub/file.jpg")]
    [InlineData("")]
    public void Remove_NonPlainFileName_IsValidationErrorAndDeletesNothing(string name)
    {
        var target = Path.Combine(_root, "important.txt");
        File.WriteAllText(target, "keep");

        var result = CreateService().RemoveImage(name);

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.True(File.Exists(target));
    }
}

internal static class PathTestExtensions
{
    public static string EnsureDirectory(this string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        return filePath;
    }
}
