using Microsoft.AspNetCore.Http;

namespace ELearningPlatform.Application.Tests.Services;

public sealed class VideoServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IVideoService Sut => _ctx.Get<IVideoService>();
    private Course _course = null!;

    public async ValueTask InitializeAsync()
    {
        _ctx = await ServiceTestContext.CreateAsync();
        var teacher = await _ctx.CreateTeacherAsync(firstName: "Vic", lastName: "Video");
        _course = await _ctx.CreateCourseAsync(teacher, "Course V");
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private void UploadSucceeds(string storedName = "stored.mp4")
        => _ctx.Media.UploadVideoAsync(Arg.Any<IFormFile>(), Arg.Any<CancellationToken>())
            .Returns(Result<MediaDto>.Success(new MediaDto { FakeName = storedName, ContentType = "video/mp4", OriginalName = "x.mp4" }));

    [Fact]
    public async Task Add_video_uploads_and_saves_it()
    {
        UploadSucceeds();

        var result = await Sut.AddVideoAsync(_course.Id, new CreateVideoDto { Title = "Intro", OriginalName = "intro.mp4", VideoFile = FormFiles.Create("intro.mp4", FormFiles.Mp4) });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("Intro", result.Data!.Title);
        Assert.Equal("stored.mp4", result.Data.VideoUrl);
        Assert.Equal("Course V", result.Data.CourseName);
        Assert.Equal("Vic Video", result.Data.TeacherName);
        Assert.Equal(_ctx.Now, result.Data.UploadedAt);
    }

    [Fact]
    public async Task Add_video_with_rejected_file_saves_nothing()
    {
        _ctx.Media.UploadVideoAsync(Arg.Any<IFormFile>(), Arg.Any<CancellationToken>()).Returns(Result<MediaDto>.Validation("bad"));

        var result = await Sut.AddVideoAsync(_course.Id, new CreateVideoDto { Title = "T", OriginalName = "o", VideoFile = FormFiles.Create("a.mp4", FormFiles.Exe) });

        Assert.Equal(ErrorCodes.InvalidFile, result.Error.Code);
        Assert.False(await _ctx.Db.Videos.AnyAsync());
    }

    [Fact]
    public async Task Add_video_to_missing_course_does_not_upload()
    {
        var result = await Sut.AddVideoAsync(999, new CreateVideoDto { Title = "T", OriginalName = "o", VideoFile = FormFiles.Create("a.mp4", FormFiles.Mp4) });

        Assert.Equal(ErrorCodes.CourseNotFound, result.Error.Code);
        await _ctx.Media.DidNotReceiveWithAnyArgs().UploadVideoAsync(default!, default);
    }

    [Fact]
    public async Task Get_by_id_requires_the_video_to_belong_to_the_course()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var otherCourse = await _ctx.CreateCourseAsync(teacher);
        var video = await _ctx.CreateVideoAsync(otherCourse);

        var result = await Sut.GetByIdAsync(_course.Id, video.Id);

        Assert.Equal(ErrorCodes.VideoNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Get_all_for_missing_course_is_not_found()
    {
        Assert.Equal(ErrorCodes.CourseNotFound, (await Sut.GetAllAsync(1234)).Error.Code);
    }

    [Fact]
    public async Task Get_all_lists_only_this_courses_videos()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var other = await _ctx.CreateCourseAsync(teacher);
        await _ctx.CreateVideoAsync(_course, "a");
        await _ctx.CreateVideoAsync(_course, "b");
        await _ctx.CreateVideoAsync(other, "c");

        var videos = (await Sut.GetAllAsync(_course.Id)).Data!;

        Assert.Equal(["a", "b"], videos.Select(v => v.Title));
    }

    [Fact]
    public async Task Pagination_of_videos()
    {
        for (var i = 0; i < 3; i++)
            await _ctx.CreateVideoAsync(_course, $"v{i}");

        var page = (await Sut.GetAllWithPaginationAsync(_course.Id, 1, 2, "https://x/v")).Data!;

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalPages);
        Assert.NotNull(page.NextPage);
        Assert.Null(page.PrevPage);
    }

    [Fact]
    public async Task Delete_removes_row_then_file()
    {
        var video = await _ctx.CreateVideoAsync(_course);

        var result = await Sut.DeleteAsync(_course.Id, video.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await _ctx.Db.Videos.AnyAsync());
        _ctx.Media.Received(1).RemoveVideo(video.VideoUrl);
    }

    [Fact]
    public async Task Delete_missing_video_does_not_touch_storage()
    {
        var result = await Sut.DeleteAsync(_course.Id, 5);

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        _ctx.Media.DidNotReceiveWithAnyArgs().RemoveVideo(default!);
    }

    [Fact]
    public async Task Download_opens_the_stored_file()
    {
        var video = await _ctx.CreateVideoAsync(_course, originalName: "lesson.mp4");
        var download = new FileDownloadDto { Content = new MemoryStream([1, 2]), ContentType = "video/mp4", DownloadName = "lesson.mp4" };
        _ctx.Media.OpenRead(Arg.Is<MediaDto>(m => m.FakeName == video.VideoUrl && m.OriginalName == "lesson.mp4"), FolderConstants.VideosFolder).Returns(download);

        var result = await Sut.DownloadVideoAsync(_course.Id, video.Id);

        Assert.Same(download, result.Data);
    }

    [Fact]
    public async Task Download_when_file_is_missing_is_not_found()
    {
        var video = await _ctx.CreateVideoAsync(_course);
        _ctx.Media.OpenRead(Arg.Any<MediaDto>(), Arg.Any<string>()).Returns((FileDownloadDto?)null);

        Assert.Equal(ErrorType.NotFound, (await Sut.DownloadVideoAsync(_course.Id, video.Id)).ErrorType);
    }

    [Fact]
    public async Task Update_metadata_only_changes_provided_fields()
    {
        var video = await _ctx.CreateVideoAsync(_course, "Old title", "old.mp4");

        await Sut.UpdateAsync(_course.Id, video.Id, new UpdateVideoDto { Title = "New title", OriginalName = null, Description = "" });

        var dto = (await Sut.GetByIdAsync(_course.Id, video.Id)).Data!;
        Assert.Equal("New title", dto.Title);
        Assert.Equal("old.mp4", dto.OriginalName);
    }

    [Fact]
    public async Task Update_file_replaces_and_removes_the_old_file_after_saving()
    {
        var video = await _ctx.CreateVideoAsync(_course);
        var oldFile = video.VideoUrl;
        UploadSucceeds("new.mp4");

        var result = await Sut.UpdateVideoAsync(_course.Id, video.Id, new UpdateVideoFileDto { VideoFile = FormFiles.Create("n.mp4", FormFiles.Mp4) });

        Assert.True(result.IsSuccess);
        Assert.Equal("new.mp4", (await Sut.GetByIdAsync(_course.Id, video.Id)).Data!.VideoUrl);
        _ctx.Media.Received(1).RemoveVideo(oldFile);
    }

    [Fact]
    public async Task Update_file_with_failed_upload_keeps_the_old_file()
    {
        var video = await _ctx.CreateVideoAsync(_course);
        _ctx.Media.UploadVideoAsync(Arg.Any<IFormFile>(), Arg.Any<CancellationToken>()).Returns(Result<MediaDto>.Validation("bad"));

        var result = await Sut.UpdateVideoAsync(_course.Id, video.Id, new UpdateVideoFileDto { VideoFile = FormFiles.Create("n.mp4", FormFiles.Exe) });

        Assert.False(result.IsSuccess);
        _ctx.Media.DidNotReceiveWithAnyArgs().RemoveVideo(default!);
    }

    [Fact]
    public async Task Reacting_again_replaces_the_previous_reaction()
    {
        var video = await _ctx.CreateVideoAsync(_course);
        var student = await _ctx.CreateStudentAsync();

        await Sut.ReactOnVideoAsync(student.ApplicationUserId, _course.Id, video.Id, isLike: true);
        await Sut.ReactOnVideoAsync(student.ApplicationUserId, _course.Id, video.Id, isLike: false);

        var dto = (await Sut.GetByIdAsync(_course.Id, video.Id)).Data!;
        Assert.Equal(0, dto.NumberOfLike);
        Assert.Equal(1, dto.NumberOfDislike);
    }

    [Fact]
    public async Task React_on_missing_video_is_not_found()
    {
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorType.NotFound, (await Sut.ReactOnVideoAsync(student.ApplicationUserId, _course.Id, 9, true)).ErrorType);
    }

    [Fact]
    public async Task Trending_orders_by_net_likes_and_skips_videos_without_reactions()
    {
        var popular = await _ctx.CreateVideoAsync(_course, "popular");
        var meh = await _ctx.CreateVideoAsync(_course, "meh");
        await _ctx.CreateVideoAsync(_course, "ignored");
        var s1 = await _ctx.CreateStudentAsync();
        var s2 = await _ctx.CreateStudentAsync();
        await Sut.ReactOnVideoAsync(s1.ApplicationUserId, _course.Id, popular.Id, true);
        await Sut.ReactOnVideoAsync(s2.ApplicationUserId, _course.Id, popular.Id, true);
        await Sut.ReactOnVideoAsync(s1.ApplicationUserId, _course.Id, meh.Id, false);

        var trending = (await Sut.GetTrendingVideosAsync(_course.Id)).Data!;

        Assert.Equal(["popular", "meh"], trending.Select(v => v.Title));
    }

    [Fact]
    public async Task Trending_returns_at_most_ten()
    {
        var student = await _ctx.CreateStudentAsync();
        for (var i = 0; i < VideoService.TrendingCount + 3; i++)
        {
            var v = await _ctx.CreateVideoAsync(_course, $"v{i}");
            await Sut.ReactOnVideoAsync(student.ApplicationUserId, _course.Id, v.Id, true);
        }

        Assert.Equal(VideoService.TrendingCount, (await Sut.GetTrendingVideosAsync(_course.Id)).Data!.Count);
    }

    [Theory]
    [InlineData("intro")]
    [InlineData("Intrduction")]
    [InlineData("lesson1")]
    public async Task Search_matches_title_or_original_name(string term)
    {
        await _ctx.CreateVideoAsync(_course, "Introduction", "lesson1.mp4");
        await _ctx.CreateVideoAsync(_course, "Quantum", "zz.mp4");

        var found = (await Sut.SearchByNameAsync(_course.Id, term)).Data!;

        Assert.Equal("Introduction", Assert.Single(found).Title);
    }

    [Fact]
    public async Task Search_in_missing_course_is_not_found()
    {
        Assert.Equal(ErrorType.NotFound, (await Sut.SearchByNameAsync(404, "x")).ErrorType);
    }

    [Fact]
    public async Task Comments_on_video_round_trip()
    {
        var video = await _ctx.CreateVideoAsync(_course);
        var student = await _ctx.CreateStudentAsync();

        await Sut.AddCommentToVideoAsync(student.ApplicationUserId, _course.Id, video.Id, new AddCommentDto { Comment = "great" });

        var comment = Assert.Single((await Sut.GetAllCommentsAsync(_course.Id, video.Id)).Data!);
        Assert.Equal("great", comment.Comment);
        Assert.Equal("Sam Student", comment.UserName);
        Assert.Equal(_ctx.Now, comment.CreatedAt);
    }

    [Fact]
    public async Task Comment_on_missing_video_is_not_found()
    {
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorType.NotFound, (await Sut.AddCommentToVideoAsync(student.ApplicationUserId, _course.Id, 3, new AddCommentDto { Comment = "x" })).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await Sut.GetAllCommentsAsync(_course.Id, 3)).ErrorType);
    }
}
