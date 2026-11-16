namespace ELearningPlatform.API.Controllers;

[ApiController]
[Route("api/Courses/{courseId:int}/[controller]")]
[Authorize(Policy = PoliciesConstants.AdminTeacherStudent)]
public class VideosController(IVideoService videoService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(int courseId, CancellationToken cancellationToken)
        => (await videoService.GetAllAsync(courseId, cancellationToken)).ToActionResult(this);

    [HttpGet("VideosPagination")]
    public async Task<IActionResult> GetPage(int courseId, int pageNumber = 1, int pageSize = PaginationExtensions.DefaultPageSize, CancellationToken cancellationToken = default)
        => (await videoService.GetAllWithPaginationAsync(courseId, pageNumber, pageSize, this.CurrentUrl(), cancellationToken)).ToActionResult(this);

    [HttpGet("Search")]
    public async Task<IActionResult> Search(int courseId, [FromQuery] string videoName, CancellationToken cancellationToken)
        => (await videoService.SearchByNameAsync(courseId, videoName, cancellationToken)).ToActionResult(this);

    [HttpGet("Trending")]
    public async Task<IActionResult> GetTrending(int courseId, CancellationToken cancellationToken)
        => (await videoService.GetTrendingVideosAsync(courseId, cancellationToken)).ToActionResult(this);

    [HttpGet("{videoId:int}")]
    public async Task<IActionResult> GetById(int courseId, int videoId, CancellationToken cancellationToken)
        => (await videoService.GetByIdAsync(courseId, videoId, cancellationToken)).ToActionResult(this);

    [HttpGet("{videoId:int}/download")]
    public async Task<IActionResult> Download(int courseId, int videoId, CancellationToken cancellationToken)
    {
        var result = await videoService.DownloadVideoAsync(courseId, videoId, cancellationToken);
        if (!result.IsSuccess)
            return result.ToProblem(this);

        // FileStreamResult streams and disposes the file; range requests let players seek
        var file = result.Data!;
        return File(file.Content, file.ContentType, file.DownloadName, enableRangeProcessing: true);
    }

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPost]
    public async Task<IActionResult> Create(int courseId, [FromForm] CreateVideoDto dto, CancellationToken cancellationToken)
    {
        var result = await videoService.AddVideoAsync(courseId, dto, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { courseId, videoId = result.Data!.Id }, result.Data)
            : result.ToProblem(this);
    }

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPut("{videoId:int}")]
    public async Task<IActionResult> Update(int courseId, int videoId, UpdateVideoDto dto, CancellationToken cancellationToken)
        => (await videoService.UpdateAsync(courseId, videoId, dto, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpPatch("{videoId:int}")]
    public async Task<IActionResult> UpdateFile(int courseId, int videoId, [FromForm] UpdateVideoFileDto dto, CancellationToken cancellationToken)
        => (await videoService.UpdateVideoAsync(courseId, videoId, dto, cancellationToken)).ToActionResult(this);

    [Authorize(Policy = PoliciesConstants.AdminTeacher)]
    [HttpDelete("{videoId:int}")]
    public async Task<IActionResult> Delete(int courseId, int videoId, CancellationToken cancellationToken)
        => (await videoService.DeleteAsync(courseId, videoId, cancellationToken)).ToActionResult(this);

    [HttpPost("{videoId:int}/Comment")]
    public async Task<IActionResult> Comment(int courseId, int videoId, AddCommentDto dto, CancellationToken cancellationToken)
        => (await videoService.AddCommentToVideoAsync(User.GetUserId(), courseId, videoId, dto, cancellationToken)).ToActionResult(this);

    [HttpGet("{videoId:int}/Comments")]
    public async Task<IActionResult> GetComments(int courseId, int videoId, CancellationToken cancellationToken)
        => (await videoService.GetAllCommentsAsync(courseId, videoId, cancellationToken)).ToActionResult(this);

    [HttpPost("{videoId:int}/Like")]
    public async Task<IActionResult> Like(int courseId, int videoId, CancellationToken cancellationToken)
        => (await videoService.ReactOnVideoAsync(User.GetUserId(), courseId, videoId, isLike: true, cancellationToken)).ToActionResult(this);

    [HttpPost("{videoId:int}/Dislike")]
    public async Task<IActionResult> Dislike(int courseId, int videoId, CancellationToken cancellationToken)
        => (await videoService.ReactOnVideoAsync(User.GetUserId(), courseId, videoId, isLike: false, cancellationToken)).ToActionResult(this);
}
