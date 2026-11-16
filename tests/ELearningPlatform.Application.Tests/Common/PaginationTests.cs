using ELearningPlatform.Shared.Helpers;

namespace ELearningPlatform.Application.Tests.Common;

public sealed class PaginationTests : IAsyncLifetime
{
    private const string Url = "https://api.test/items";
    private ServiceTestContext _ctx = null!;
    private IQueryable<Course> Courses => _ctx.Db.Courses.OrderBy(c => c.Id);

    public async ValueTask InitializeAsync()
    {
        _ctx = await ServiceTestContext.CreateAsync();
        var teacher = await _ctx.CreateTeacherAsync();
        for (var i = 1; i <= 25; i++)
            await _ctx.CreateCourseAsync(teacher, $"C{i}");
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    [Theory]
    [InlineData(1, 10, 10, 3)]
    [InlineData(3, 10, 5, 3)]
    [InlineData(1, 25, 25, 1)]
    [InlineData(2, 7, 7, 4)]
    [InlineData(4, 7, 4, 4)]
    public async Task Page_sizes_and_counts(int page, int size, int expectedItems, int expectedPages)
    {
        var result = await Courses.ToPaginatedResultAsync(page, size, Url);

        Assert.Equal(expectedItems, result.Items.Count);
        Assert.Equal(expectedPages, result.TotalPages);
        Assert.Equal(25, result.TotalCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Non_positive_page_number_becomes_one(int page)
    {
        var result = await Courses.ToPaginatedResultAsync(page, 10, Url);

        Assert.Equal(1, result.CurrentPage);
        Assert.Equal("C1", result.Items[0].Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_page_size_uses_default(int size)
    {
        var result = await Courses.ToPaginatedResultAsync(1, size, Url);

        Assert.Equal(PaginationExtensions.DefaultPageSize, result.PageSize);
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        var result = await Courses.ToPaginatedResultAsync(1, 10_000, Url);

        Assert.Equal(PaginationExtensions.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task First_page_has_only_next_link()
    {
        var result = await Courses.ToPaginatedResultAsync(1, 10, Url);

        Assert.Null(result.PrevPage);
        Assert.Equal($"{Url}?pageNumber=2&pageSize=10", result.NextPage);
    }

    [Fact]
    public async Task Last_page_has_only_previous_link()
    {
        var result = await Courses.ToPaginatedResultAsync(3, 10, Url);

        Assert.Null(result.NextPage);
        Assert.Equal($"{Url}?pageNumber=2&pageSize=10", result.PrevPage);
    }

    [Fact]
    public async Task Page_past_the_end_is_empty()
    {
        var result = await Courses.ToPaginatedResultAsync(9, 10, Url);

        Assert.Empty(result.Items);
        Assert.Null(result.NextPage);
    }

    [Fact]
    public async Task Empty_query_has_zero_pages()
    {
        var result = await _ctx.Db.Courses.Where(c => c.Id < 0).ToPaginatedResultAsync(1, 10, Url);

        Assert.Equal(0, result.TotalPages);
        Assert.Null(result.NextPage);
        Assert.Null(result.PrevPage);
    }

    [Fact]
    public void Page_url_format()
    {
        Assert.Equal("https://x/y?pageNumber=3&pageSize=20", PaginationHelper.BuildPageUrl("https://x/y", 3, 20));
    }
}
