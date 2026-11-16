namespace ELearningPlatform.Application.Tests.Services;

public sealed class CourseServiceTests : IAsyncLifetime
{
    private const string BaseUrl = "https://api.test/api/Courses/CoursesPagination";
    private ServiceTestContext _ctx = null!;
    private ICourseService Sut => _ctx.Get<ICourseService>();

    public async ValueTask InitializeAsync() => _ctx = await ServiceTestContext.CreateAsync();
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    [Fact]
    public async Task Teacher_can_add_a_course()
    {
        var teacher = await _ctx.CreateTeacherAsync(Specialization.Mathematics, "Ada", "Lovelace");

        var result = await Sut.AddAsync(teacher.ApplicationUserId, new CreateCourseDto { Name = "Algebra", Description = "d", Hours = 10, Price = 49.99m });

        Assert.True(result.IsSuccess, result.Message);
        var dto = result.Data!;
        Assert.Equal("Algebra", dto.Name);
        Assert.Equal(49.99m, dto.Price);
        Assert.False(dto.IsFree);
        Assert.Equal("Ada Lovelace", dto.TeacherName);
        Assert.Equal(Specialization.Mathematics, dto.Specialization);
        Assert.Equal(_ctx.Now, dto.CreateAt);
    }

    [Fact]
    public async Task Non_teacher_cannot_add_a_course()
    {
        var student = await _ctx.CreateStudentAsync();

        var result = await Sut.AddAsync(student.ApplicationUserId, new CreateCourseDto { Name = "X", Hours = 1 });

        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.False(await _ctx.Db.Courses.AnyAsync());
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0.01, false)]
    [InlineData(1000, false)]
    public async Task IsFree_follows_the_price(double price, bool expectedFree)
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, price: (decimal)price);

        var dto = (await Sut.GetByIdAsync(course.Id)).Data!;

        Assert.Equal(expectedFree, dto.IsFree);
    }

    [Fact]
    public async Task Decimal_price_survives_the_database_exactly()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, price: 19.99m);

        var price = await _ctx.InNewScopeAsync(sp => sp.GetRequiredService<AppDbContext>().Courses.Where(c => c.Id == course.Id).Select(c => c.Price).SingleAsync());

        Assert.Equal(19.99m, price);
    }

    [Fact]
    public async Task Get_by_id_counts_videos_comments_and_reactions()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher);
        await _ctx.CreateVideoAsync(course);
        await _ctx.CreateVideoAsync(course);
        var s1 = await _ctx.CreateStudentAsync();
        var s2 = await _ctx.CreateStudentAsync();
        var s3 = await _ctx.CreateStudentAsync();
        await Sut.RateCourseAsync(s1.ApplicationUserId, course.Id, new CourseReactionDto { IsLike = true, Rating = Rating.FiveStars });
        await Sut.RateCourseAsync(s2.ApplicationUserId, course.Id, new CourseReactionDto { IsLike = true, Rating = Rating.FourStars });
        await Sut.RateCourseAsync(s3.ApplicationUserId, course.Id, new CourseReactionDto { IsLike = false, Rating = Rating.OneStar });
        await Sut.AddCommentToCourseAsync(s1.ApplicationUserId, course.Id, new AddCommentDto { Comment = "nice" });

        var dto = (await Sut.GetByIdAsync(course.Id)).Data!;

        Assert.Equal(2, dto.NumberOfVides);
        Assert.Equal(2, dto.NumberOfLike);
        Assert.Equal(1, dto.NumberOfDislike);
        Assert.Equal(1, dto.NumberOfComment);
    }

    [Fact]
    public async Task Get_by_id_for_missing_course_is_not_found()
    {
        var result = await Sut.GetByIdAsync(404);

        Assert.Equal(ErrorCodes.CourseNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Get_all_returns_courses_in_id_order()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var a = await _ctx.CreateCourseAsync(teacher, "A");
        var b = await _ctx.CreateCourseAsync(teacher, "B");

        var all = (await Sut.GetAllAsync()).Data!;

        Assert.Equal([a.Id, b.Id], all.Select(c => c.Id));
    }

    [Fact]
    public async Task Get_all_on_empty_database_is_empty_success()
    {
        var result = await Sut.GetAllAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Pagination_returns_page_and_links()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        for (var i = 0; i < 5; i++)
            await _ctx.CreateCourseAsync(teacher, $"C{i}");

        var page = (await Sut.GetAllWithPaginationsAsync(2, 2, BaseUrl)).Data!;

        Assert.Equal(["C2", "C3"], page.Items.Select(c => c.Name));
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal($"{BaseUrl}?pageNumber=3&pageSize=2", page.NextPage);
        Assert.Equal($"{BaseUrl}?pageNumber=1&pageSize=2", page.PrevPage);
    }

    [Fact]
    public async Task Update_changes_only_provided_fields()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, "Old", hours: 5);

        var result = await Sut.UpdateAsync(course.Id, new UpdateCourseDto { Name = "New", Description = "  " });

        Assert.True(result.IsSuccess);
        var dto = (await Sut.GetByIdAsync(course.Id)).Data!;
        Assert.Equal("New", dto.Name);
        Assert.Equal("Desc", dto.Description);
        Assert.Equal(5, dto.Hours);
    }

    [Fact]
    public async Task Update_hours()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher);

        await Sut.UpdateAsync(course.Id, new UpdateCourseDto { Hours = 42 });

        Assert.Equal(42, (await Sut.GetByIdAsync(course.Id)).Data!.Hours);
    }

    [Fact]
    public async Task Update_missing_course_is_not_found()
    {
        var result = await Sut.UpdateAsync(1, new UpdateCourseDto { Name = "x" });

        Assert.Equal(ErrorCodes.CourseNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Update_price()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher, price: 10m);

        await Sut.UpdatePriceAsync(course.Id, new UpdateCoursePriceDto { NewPrice = 0m });

        Assert.True((await Sut.GetByIdAsync(course.Id)).Data!.IsFree);
    }

    [Fact]
    public async Task Update_price_of_missing_course_is_not_found()
    {
        var result = await Sut.UpdatePriceAsync(1, new UpdateCoursePriceDto { NewPrice = 1m });

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Delete_removes_the_course()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher);

        var result = await Sut.DeleteAsync(course.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, (await Sut.GetByIdAsync(course.Id)).ErrorType);
    }

    [Fact]
    public async Task Delete_missing_course_is_not_found()
    {
        Assert.Equal(ErrorType.NotFound, (await Sut.DeleteAsync(77)).ErrorType);
    }

    [Theory]
    [InlineData("Machine Learning")]
    [InlineData("machine learning")]
    [InlineData("Machin Lerning")]
    [InlineData("learning")]
    public async Task Search_by_name_tolerates_case_and_typos(string term)
    {
        var teacher = await _ctx.CreateTeacherAsync();
        await _ctx.CreateCourseAsync(teacher, "Machine Learning");
        await _ctx.CreateCourseAsync(teacher, "Ancient History");

        var found = (await Sut.SearchByNameAsync(term)).Data!;

        Assert.Equal("Machine Learning", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Search_by_name_orders_best_match_first()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        await _ctx.CreateCourseAsync(teacher, "Physics for Engineers");
        await _ctx.CreateCourseAsync(teacher, "Physics");

        var found = (await Sut.SearchByNameAsync("Physics")).Data!;

        Assert.Equal(2, found.Count);
        Assert.All(found, c => Assert.Contains("Physics", c.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("zzzzqqqq")]
    public async Task Search_by_name_without_match_is_empty(string term)
    {
        var teacher = await _ctx.CreateTeacherAsync();
        await _ctx.CreateCourseAsync(teacher, "Chemistry");

        var result = await Sut.SearchByNameAsync(term);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Search_by_specialization_filters_by_teacher()
    {
        var math = await _ctx.CreateTeacherAsync(Specialization.Mathematics);
        var bio = await _ctx.CreateTeacherAsync(Specialization.Biology);
        await _ctx.CreateCourseAsync(math, "Calculus");
        await _ctx.CreateCourseAsync(bio, "Cells");

        var found = (await Sut.SearchBySpecializationAsync(Specialization.Biology)).Data!;

        Assert.Equal("Cells", Assert.Single(found).Name);
    }

    [Fact]
    public async Task Rate_twice_conflicts()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher);
        var student = await _ctx.CreateStudentAsync();
        await Sut.RateCourseAsync(student.ApplicationUserId, course.Id, new CourseReactionDto { IsLike = true, Rating = Rating.FiveStars });

        var second = await Sut.RateCourseAsync(student.ApplicationUserId, course.Id, new CourseReactionDto { IsLike = false, Rating = Rating.OneStar });

        Assert.Equal(ErrorCodes.AlreadyRated, second.Error.Code);
    }

    [Fact]
    public async Task Rate_missing_course_is_not_found()
    {
        var student = await _ctx.CreateStudentAsync();

        var result = await Sut.RateCourseAsync(student.ApplicationUserId, 99, new CourseReactionDto { IsLike = true, Rating = Rating.FiveStars });

        Assert.Equal(ErrorCodes.CourseNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Rate_stores_reaction_time_from_the_clock()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher);
        var student = await _ctx.CreateStudentAsync();

        await Sut.RateCourseAsync(student.ApplicationUserId, course.Id, new CourseReactionDto { IsLike = true, Rating = Rating.ThreeStars });

        var reaction = await _ctx.Db.CourseReactions.SingleAsync();
        Assert.Equal(_ctx.Now, reaction.ReactedAt);
        Assert.Equal(Rating.ThreeStars, reaction.Rating);
    }

    [Fact]
    public async Task Top_rated_picks_the_highest_average()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var good = await _ctx.CreateCourseAsync(teacher, "Good");
        var bad = await _ctx.CreateCourseAsync(teacher, "Bad");
        var s1 = await _ctx.CreateStudentAsync();
        var s2 = await _ctx.CreateStudentAsync();
        await Sut.RateCourseAsync(s1.ApplicationUserId, good.Id, new CourseReactionDto { IsLike = true, Rating = Rating.FiveStars });
        await Sut.RateCourseAsync(s2.ApplicationUserId, good.Id, new CourseReactionDto { IsLike = true, Rating = Rating.FourStars });
        await Sut.RateCourseAsync(s1.ApplicationUserId, bad.Id, new CourseReactionDto { IsLike = false, Rating = Rating.TwoStars });

        var top = await Sut.GetTopRatedCourseAsync();

        Assert.Equal("Good", top.Data!.Name);
    }

    [Fact]
    public async Task Top_rated_without_ratings_is_not_found()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        await _ctx.CreateCourseAsync(teacher);

        Assert.Equal(ErrorType.NotFound, (await Sut.GetTopRatedCourseAsync()).ErrorType);
    }

    [Fact]
    public async Task Comments_are_returned_newest_first_with_author_name()
    {
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher);
        var student = await _ctx.CreateStudentAsync();
        await Sut.AddCommentToCourseAsync(student.ApplicationUserId, course.Id, new AddCommentDto { Comment = "first" });
        _ctx.Time.Advance(TimeSpan.FromMinutes(1));
        await Sut.AddCommentToCourseAsync(student.ApplicationUserId, course.Id, new AddCommentDto { Comment = "second" });

        var comments = (await Sut.GetAllCommentsAsync(course.Id)).Data!;

        Assert.Equal(["second", "first"], comments.Select(c => c.Comment));
        Assert.All(comments, c => Assert.Equal("Sam Student", c.UserName));
    }

    [Fact]
    public async Task Comment_on_missing_course_is_not_found()
    {
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorType.NotFound, (await Sut.AddCommentToCourseAsync(student.ApplicationUserId, 5, new AddCommentDto { Comment = "x" })).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await Sut.GetAllCommentsAsync(5)).ErrorType);
    }

    [Fact]
    public async Task Cancelled_token_stops_the_query()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sut.GetAllAsync(cts.Token));
    }
}
