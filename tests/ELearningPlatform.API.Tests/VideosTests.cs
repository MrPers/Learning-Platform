using System.Net;
using System.Net.Http.Headers;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.Common.Results;
using ELearningPlatform.Application.DTOs.Media;
using ELearningPlatform.Application.DTOs.React;

namespace ELearningPlatform.API.Tests;

public class VideosTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(TestUser Teacher, int CourseId)> CreateCourseAsync()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync(name: "Video course");
        return (teacher, course.Id);
    }

    [Fact]
    public async Task Upload_RealMp4_Returns201AndStoresFile()
    {
        var (teacher, courseId) = await CreateCourseAsync();

        var response = await teacher.Client.PostAsync($"api/Courses/{courseId}/Videos", TestData.VideoForm(TestData.Mp4, title: "Intro"));

        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        var video = await response.ReadAsync<VideoDto>();
        Assert.Equal("Intro", video.Title);
        Assert.Equal("video/mp4", video.ContentType);
        Assert.Equal("Video course", video.CourseName);
        Assert.EndsWith(".mp4", video.VideoUrl);
        Assert.True(File.Exists(Path.Combine(factory.WebRoot, "Uploads", "Videos", video.VideoUrl)));
        Assert.NotNull(response.Headers.Location);
    }

    [Theory]
    [InlineData("evil.mp4")]
    [InlineData("evil.mkv")]
    public async Task Upload_ExecutableRenamedAsVideo_Returns400(string fileName)
    {
        var (teacher, courseId) = await CreateCourseAsync();

        var response = await teacher.Client.PostAsync($"api/Courses/{courseId}/Videos", TestData.VideoForm(TestData.Exe, fileName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("VideoFile", await response.ReadValidationErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_DisallowedExtension_Returns400()
    {
        var (teacher, courseId) = await CreateCourseAsync();

        var response = await teacher.Client.PostAsync($"api/Courses/{courseId}/Videos", TestData.VideoForm(TestData.Mp4, "clip.exe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_TooLarge_Returns400()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var big = new byte[6 * 1024 * 1024];
        TestData.Mp4.CopyTo(big, 0);

        var response = await teacher.Client.PostAsync($"api/Courses/{courseId}/Videos", TestData.VideoForm(big));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutFile_Returns400()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var form = new MultipartFormDataContent
        {
            { new StringContent("Title"), "Title" },
            { new StringContent("x.mp4"), "OriginalName" },
        };

        var response = await teacher.Client.PostAsync($"api/Courses/{courseId}/Videos", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ToUnknownCourse_Returns404()
    {
        var teacher = await factory.CreateUserAsync("Teacher");

        var response = await teacher.Client.PostAsync("api/Courses/987654/Videos", TestData.VideoForm(TestData.Mp4));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_AsStudent_Returns403()
    {
        var student = await factory.CreateUserAsync("Student");

        var response = await student.Client.PostAsync("api/Courses/1/Videos", TestData.VideoForm(TestData.Mp4));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Download_ReturnsBytesAndContentType()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId);

        var response = await teacher.Client.GetAsync($"api/Courses/{courseId}/Videos/{video.Id}/download");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
        Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TestData.Mp4, await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("lesson.mp4", response.Content.Headers.ContentDisposition?.ToString());
        Assert.Contains("bytes", response.Headers.AcceptRanges);
    }

    [Fact]
    public async Task Download_RangeRequest_Returns206WithPartialContent()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId);
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/Courses/{courseId}/Videos/{video.Id}/download");
        request.Headers.Range = new RangeHeaderValue(4, 7);

        var response = await teacher.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("ftyp"u8.ToArray(), await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(TestData.Mp4.Length, response.Content.Headers.ContentRange?.Length);
    }

    [Fact]
    public async Task Download_FileMissingFromStorage_Returns404()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId);
        File.Delete(Path.Combine(factory.WebRoot, "Uploads", "Videos", video.VideoUrl));

        var response = await teacher.Client.GetAsync($"api/Courses/{courseId}/Videos/{video.Id}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesRowAndFile()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId);
        var path = Path.Combine(factory.WebRoot, "Uploads", "Videos", video.VideoUrl);

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await teacher.Client.DeleteAsync($"api/Courses/{courseId}/Videos/{video.Id}"));

        Assert.False(File.Exists(path));
        var get = await teacher.Client.GetAsync($"api/Courses/{courseId}/Videos/{video.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal("Video.NotFound", await get.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task UpdateMetadata_ChangesTitle()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId);

        await ResponseAssert.StatusAsync(HttpStatusCode.OK,
            await teacher.Client.PutJsonAsync($"api/Courses/{courseId}/Videos/{video.Id}", new { title = "Renamed" }));

        Assert.Equal("Renamed", (await (await teacher.Client.GetAsync($"api/Courses/{courseId}/Videos/{video.Id}")).ReadAsync<VideoDto>()).Title);
    }

    [Fact]
    public async Task ReplaceFile_SwapsStoredFileAndDeletesOldOne()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId);
        var oldPath = Path.Combine(factory.WebRoot, "Uploads", "Videos", video.VideoUrl);
        var form = new MultipartFormDataContent { { TestData.File(TestData.Mp4, "video/mp4"), "VideoFile", "v2.mp4" } };

        var response = await teacher.Client.PatchAsync($"api/Courses/{courseId}/Videos/{video.Id}", form);

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, response);
        var updated = await (await teacher.Client.GetAsync($"api/Courses/{courseId}/Videos/{video.Id}")).ReadAsync<VideoDto>();
        Assert.NotEqual(video.VideoUrl, updated.VideoUrl);
        Assert.False(File.Exists(oldPath));
    }

    [Fact]
    public async Task LikeAndDislike_ReplaceEachOtherPerUser()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId);
        var student = await factory.CreateUserAsync("Student");

        await student.Client.PostAsync($"api/Courses/{courseId}/Videos/{video.Id}/Like", null);
        var afterLike = await (await student.Client.GetAsync($"api/Courses/{courseId}/Videos/{video.Id}")).ReadAsync<VideoDto>();
        await student.Client.PostAsync($"api/Courses/{courseId}/Videos/{video.Id}/Dislike", null);
        var afterDislike = await (await student.Client.GetAsync($"api/Courses/{courseId}/Videos/{video.Id}")).ReadAsync<VideoDto>();

        Assert.Equal((1, 0), (afterLike.NumberOfLike, afterLike.NumberOfDislike));
        Assert.Equal((0, 1), (afterDislike.NumberOfLike, afterDislike.NumberOfDislike));
    }

    [Fact]
    public async Task Like_UnknownVideo_Returns404()
    {
        var (teacher, courseId) = await CreateCourseAsync();

        var response = await teacher.Client.PostAsync($"api/Courses/{courseId}/Videos/987654/Like", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Trending_OrdersByNetLikes_AndSkipsUnreactedVideos()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var popular = await teacher.UploadVideoAsync(courseId, "popular");
        var disliked = await teacher.UploadVideoAsync(courseId, "disliked");
        var ignored = await teacher.UploadVideoAsync(courseId, "ignored");
        var s1 = await factory.CreateUserAsync("Student");
        var s2 = await factory.CreateUserAsync("Student");
        await s1.Client.PostAsync($"api/Courses/{courseId}/Videos/{popular.Id}/Like", null);
        await s2.Client.PostAsync($"api/Courses/{courseId}/Videos/{popular.Id}/Like", null);
        await s1.Client.PostAsync($"api/Courses/{courseId}/Videos/{disliked.Id}/Dislike", null);

        var trending = await (await teacher.Client.GetAsync($"api/Courses/{courseId}/Videos/Trending")).ReadAsync<List<VideoDto>>();

        Assert.Equal([popular.Id, disliked.Id], trending.Select(v => v.Id));
        Assert.DoesNotContain(trending, v => v.Id == ignored.Id);
    }

    [Fact]
    public async Task Comments_AddAndList()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId);
        var student = await factory.CreateUserAsync("Student");

        await ResponseAssert.StatusAsync(HttpStatusCode.OK,
            await student.Client.PostJsonAsync($"api/Courses/{courseId}/Videos/{video.Id}/Comment", new { comment = "Nice" }));
        var comments = await (await student.Client.GetAsync($"api/Courses/{courseId}/Videos/{video.Id}/Comments")).ReadAsync<List<CommentDto>>();

        Assert.Equal("Nice", Assert.Single(comments).Comment);
    }

    [Fact]
    public async Task Search_FindsByTitleWithTypo()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        var video = await teacher.UploadVideoAsync(courseId, "Dependency Injection Deep Dive");
        await teacher.UploadVideoAsync(courseId, "Cooking pasta");

        var results = await (await teacher.Client.GetAsync($"api/Courses/{courseId}/Videos/Search?videoName=Dependncy%20Injection"))
            .ReadAsync<List<VideoDto>>();

        Assert.Equal(video.Id, results.First().Id);
    }

    [Fact]
    public async Task Pagination_ListsVideosOfCourse()
    {
        var (teacher, courseId) = await CreateCourseAsync();
        for (var i = 0; i < 3; i++)
            await teacher.UploadVideoAsync(courseId, $"v{i}");

        var page = await (await teacher.Client.GetAsync($"api/Courses/{courseId}/Videos/VideosPagination?pageNumber=1&pageSize=2"))
            .ReadAsync<PaginatedResult<VideoDto>>();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal($"https://localhost/api/Courses/{courseId}/Videos/VideosPagination?pageNumber=2&pageSize=2", page.NextPage);
    }

    [Fact]
    public async Task GetAll_ForUnknownCourse_Returns404()
    {
        var teacher = await factory.CreateUserAsync("Teacher");

        var response = await teacher.Client.GetAsync("api/Courses/987654/Videos");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Course.NotFound", await response.ReadProblemCodeAsync());
    }
}
