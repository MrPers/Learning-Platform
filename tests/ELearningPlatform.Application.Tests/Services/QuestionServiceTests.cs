namespace ELearningPlatform.Application.Tests.Services;

public sealed class QuestionServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IQuestionService Sut => _ctx.Get<IQuestionService>();
    private Exam _exam = null!;

    public async ValueTask InitializeAsync()
    {
        _ctx = await ServiceTestContext.CreateAsync();
        var teacher = await _ctx.CreateTeacherAsync();
        var course = await _ctx.CreateCourseAsync(teacher);
        _exam = await _ctx.CreateExamAsync(course, teacher, withQuestions: false);
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private static CreateMcqQuestionDto Mcq(int correct = 3) => new()
    {
        Header = "Pick one", Mark = 2, Option1 = "a", Option2 = "b", Option3 = "c", Option4 = "d", CorrectAnswerIndex = correct,
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Mcq_marks_exactly_the_chosen_option_correct(int correct)
    {
        var result = await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq(correct));

        var options = result.Data!.AnswerOptions.ToList();
        Assert.Equal(4, options.Count);
        Assert.Equal([1, 2, 3, 4], options.Select(o => (int)o.OptionNumber));
        Assert.Equal(correct, Assert.Single(options, o => o.IsCorrect).OptionNumber);
        Assert.Equal(QuestionType.MultipleChoice, result.Data.QuestionType);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task True_false_question_has_two_options(bool isTrue, int correctOption)
    {
        var result = await Sut.CreateTFQuestionAsync(_exam.Id, new CreateTFQuestionDto { Header = "Sky is blue", Mark = 1, IsTrue = isTrue });

        var options = result.Data!.AnswerOptions.ToList();
        Assert.Equal(["True", "False"], options.Select(o => o.Text));
        Assert.Equal(correctOption, Assert.Single(options, o => o.IsCorrect).OptionNumber);
        Assert.Equal(QuestionType.TrueFalse, result.Data.QuestionType);
    }

    [Fact]
    public async Task Questions_are_numbered_sequentially()
    {
        await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());
        await Sut.CreateTFQuestionAsync(_exam.Id, new CreateTFQuestionDto { Header = "h", Mark = 1 });
        var third = await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());

        Assert.Equal(3, third.Data!.QuestionNumber);
    }

    [Fact]
    public async Task Numbering_continues_after_the_highest_number_when_a_question_is_deleted()
    {
        await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());
        await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());
        await Sut.DeleteQuestionAsync(_exam.Id, 1);

        var next = await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());

        Assert.Equal(3, next.Data!.QuestionNumber);
    }

    [Fact]
    public async Task Question_limit_is_enforced()
    {
        _ctx.Db.Questions.Add(new Question { ExamId = _exam.Id, QuestionNumber = byte.MaxValue, Header = "last", Mark = 1 });
        await _ctx.Db.SaveChangesAsync();

        var result = await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());

        Assert.Equal(ErrorCodes.QuestionLimitReached, result.Error.Code);
    }

    [Fact]
    public async Task Create_for_missing_exam_is_not_found()
    {
        Assert.Equal(ErrorCodes.ExamNotFound, (await Sut.CreateMcqQuestionAsync(999, Mcq())).Error.Code);
        Assert.Equal(ErrorCodes.ExamNotFound, (await Sut.CreateTFQuestionAsync(999, new CreateTFQuestionDto { Header = "h", Mark = 1 })).Error.Code);
    }

    [Fact]
    public async Task Get_all_is_ordered_by_number()
    {
        await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());
        await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());

        var all = (await Sut.GetAllQuestionAsync(_exam.Id)).Data!;

        Assert.Equal([1, 2], all.Select(q => (int)q.QuestionNumber));
    }

    [Fact]
    public async Task Get_all_for_missing_exam_is_not_found()
    {
        Assert.Equal(ErrorType.NotFound, (await Sut.GetAllQuestionAsync(5)).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await Sut.GetAllQuestionWithPaginationAsync("u", 5, 1, 10)).ErrorType);
    }

    [Fact]
    public async Task Pagination_of_questions()
    {
        for (var i = 0; i < 3; i++)
            await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());

        var page = (await Sut.GetAllQuestionWithPaginationAsync("https://x/q", _exam.Id, 2, 2)).Data!;

        Assert.Equal(3, Assert.Single(page.Items).QuestionNumber);
        Assert.Null(page.NextPage);
        Assert.NotNull(page.PrevPage);
    }

    [Fact]
    public async Task Update_mark()
    {
        await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());

        await Sut.UpdateQuestionDegreeAsync(_exam.Id, 1, new UpdateQuestionDegreeDto { Mark = 7.5 });

        Assert.Equal(7.5, (await Sut.GetQuestionByIdAsync(_exam.Id, 1)).Data!.Mark);
    }

    [Fact]
    public async Task Update_delete_get_missing_question_is_not_found()
    {
        Assert.Equal(ErrorCodes.QuestionNotFound, (await Sut.UpdateQuestionDegreeAsync(_exam.Id, 1, new UpdateQuestionDegreeDto { Mark = 1 })).Error.Code);
        Assert.Equal(ErrorCodes.QuestionNotFound, (await Sut.DeleteQuestionAsync(_exam.Id, 1)).Error.Code);
        Assert.Equal(ErrorCodes.QuestionNotFound, (await Sut.GetQuestionByIdAsync(_exam.Id, 1)).Error.Code);
    }

    [Fact]
    public async Task Delete_removes_question_and_options()
    {
        await Sut.CreateMcqQuestionAsync(_exam.Id, Mcq());

        await Sut.DeleteQuestionAsync(_exam.Id, 1);

        Assert.False(await _ctx.Db.Questions.AnyAsync());
        Assert.False(await _ctx.Db.AnswerOptions.AnyAsync());
    }
}
