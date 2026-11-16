namespace ELearningPlatform.Application.Tests.Services;

public sealed class ExamServiceTests : IAsyncLifetime
{
    private ServiceTestContext _ctx = null!;
    private IExamService Sut => _ctx.Get<IExamService>();
    private Teacher _teacher = null!;
    private Course _course = null!;

    public async ValueTask InitializeAsync()
    {
        _ctx = await ServiceTestContext.CreateAsync();
        _teacher = await _ctx.CreateTeacherAsync(firstName: "Eve", lastName: "Exam");
        _course = await _ctx.CreateCourseAsync(_teacher, "Exam Course");
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private static List<SubmitExamDto> Answers(params (byte q, byte o)[] answers)
        => answers.Select(a => new SubmitExamDto { QuestionNumber = a.q, OptionNumber = a.o }).ToList();

    [Fact]
    public async Task Teacher_creates_exam_and_start_is_stored_in_utc()
    {
        var start = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        var result = await Sut.CreateAsync(_teacher.ApplicationUserId, _course.Id, new CreateExamDto { Title = "Final", DurationInMinutes = 90, StartAt = start });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(start, result.Data!.StartAt);
        Assert.Equal(DateTimeKind.Utc, result.Data.StartAt.Kind);
        Assert.Equal(start.AddMinutes(90), result.Data.EndAt);
        Assert.Equal("Exam Course", result.Data.CourseName);
        Assert.Equal("Eve Exam", result.Data.TeacherName);
    }

    [Fact]
    public async Task Student_cannot_create_exam()
    {
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorType.Forbidden, (await Sut.CreateAsync(student.ApplicationUserId, _course.Id, new CreateExamDto { Title = "x", DurationInMinutes = 1 })).ErrorType);
    }

    [Fact]
    public async Task Create_exam_for_missing_course_is_not_found()
    {
        Assert.Equal(ErrorCodes.CourseNotFound, (await Sut.CreateAsync(_teacher.ApplicationUserId, 999, new CreateExamDto { Title = "x", DurationInMinutes = 1 })).Error.Code);
    }

    [Fact]
    public async Task Exam_dto_computes_question_totals()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);

        var dto = (await Sut.GetByIdAsync(_course.Id, exam.Id)).Data!;

        Assert.Equal(2, dto.NumberOfQuestions);
        Assert.Equal(3, dto.TotalMark);
        Assert.Equal(1.5, dto.PassMark);
    }

    [Fact]
    public async Task Exam_without_questions_has_zero_totals()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher, withQuestions: false);

        var dto = (await Sut.GetByIdAsync(_course.Id, exam.Id)).Data!;

        Assert.Equal(0, dto.NumberOfQuestions);
        Assert.Equal(0, dto.TotalMark);
    }

    [Fact]
    public async Task Get_all_orders_by_start_time()
    {
        var later = await _ctx.CreateExamAsync(_course, _teacher, _ctx.Now.AddDays(2));
        var sooner = await _ctx.CreateExamAsync(_course, _teacher, _ctx.Now.AddDays(1));

        var exams = (await Sut.GetAllAsync(_course.Id)).Data!;

        Assert.Equal([sooner.Id, later.Id], exams.Select(e => e.Id));
    }

    [Fact]
    public async Task Get_exam_through_wrong_course_is_not_found()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var other = await _ctx.CreateCourseAsync(_teacher);

        Assert.Equal(ErrorCodes.ExamNotFound, (await Sut.GetByIdAsync(other.Id, exam.Id)).Error.Code);
    }

    [Fact]
    public async Task Get_all_for_missing_course_is_not_found()
    {
        Assert.Equal(ErrorType.NotFound, (await Sut.GetAllAsync(99)).ErrorType);
    }

    [Fact]
    public async Task Paper_lists_questions_and_options_without_correct_answers()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var student = await _ctx.CreateStudentAsync();

        var result = await Sut.GetPaperAsync(student.ApplicationUserId, _course.Id, exam.Id);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, result.Data!.Questions.Count);
        Assert.All(result.Data.Questions, q => Assert.NotEmpty(q.Options));
        Assert.DoesNotContain("isCorrect", System.Text.Json.JsonSerializer.Serialize(result.Data), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Paper_before_start_is_rejected()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher, _ctx.Now.AddMinutes(10));
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorCodes.ExamNotStarted, (await Sut.GetPaperAsync(student.ApplicationUserId, _course.Id, exam.Id)).Error.Code);
    }

    [Fact]
    public async Task Paper_after_end_is_rejected()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher, _ctx.Now.AddMinutes(-5), durationInMinutes: 30);
        var student = await _ctx.CreateStudentAsync();
        _ctx.Time.Advance(TimeSpan.FromMinutes(26));

        Assert.Equal(ErrorCodes.ExamEnded, (await Sut.GetPaperAsync(student.ApplicationUserId, _course.Id, exam.Id)).Error.Code);
    }

    [Fact]
    public async Task Paper_after_submission_conflicts()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var student = await _ctx.CreateStudentAsync();
        await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2)));

        Assert.Equal(ErrorCodes.ExamAlreadySubmitted, (await Sut.GetPaperAsync(student.ApplicationUserId, _course.Id, exam.Id)).Error.Code);
    }

    [Fact]
    public async Task Paper_is_only_for_students_and_missing_exam_is_not_found()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorType.Forbidden, (await Sut.GetPaperAsync(_teacher.ApplicationUserId, _course.Id, exam.Id)).ErrorType);
        Assert.Equal(ErrorCodes.ExamNotFound, (await Sut.GetPaperAsync(student.ApplicationUserId, _course.Id, 42)).Error.Code);
    }

    [Fact]
    public async Task Perfect_submission_scores_full_marks()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var student = await _ctx.CreateStudentAsync();

        var result = await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2), (2, 1)));

        Assert.True(result.IsSuccess, result.Message);
        var stored = await _ctx.Db.StudentExams.Include(s => s.StudentAnswers).SingleAsync();
        Assert.Equal(3, stored.Degree);
        Assert.Equal(3, stored.TotalDegree);
        Assert.Equal(2, stored.StudentAnswers.Count);
        Assert.Equal(_ctx.Now, stored.SubmittedAt);
    }

    [Fact]
    public async Task Wrong_answers_score_nothing()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var student = await _ctx.CreateStudentAsync();

        await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 1), (2, 2)));

        Assert.Equal(0, (await _ctx.Db.StudentExams.SingleAsync()).Degree);
    }

    [Fact]
    public async Task Submit_before_start_is_rejected()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher, _ctx.Now.AddMinutes(10));
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorCodes.ExamNotStarted, (await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2)))).Error.Code);
    }

    [Fact]
    public async Task Submit_after_end_is_rejected()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher, _ctx.Now.AddMinutes(-5), durationInMinutes: 30);
        var student = await _ctx.CreateStudentAsync();
        _ctx.Time.Advance(TimeSpan.FromMinutes(26));

        Assert.Equal(ErrorCodes.ExamEnded, (await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2)))).Error.Code);
    }

    [Fact]
    public async Task Submit_exactly_at_the_end_is_still_accepted()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher, _ctx.Now, durationInMinutes: 30);
        var student = await _ctx.CreateStudentAsync();
        _ctx.Time.Advance(TimeSpan.FromMinutes(30));

        Assert.True((await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2)))).IsSuccess);
    }

    [Fact]
    public async Task Second_submission_conflicts()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var student = await _ctx.CreateStudentAsync();
        await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2)));

        Assert.Equal(ErrorCodes.ExamAlreadySubmitted, (await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2)))).Error.Code);
    }

    [Fact]
    public async Task Teacher_cannot_submit()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);

        Assert.Equal(ErrorType.Forbidden, (await Sut.SubmitExamAsync(_teacher.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2)))).ErrorType);
    }

    [Fact]
    public async Task Submit_to_missing_exam_is_not_found()
    {
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorCodes.ExamNotFound, (await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, 42, Answers((1, 2)))).Error.Code);
    }

    [Fact]
    public async Task Result_reports_percentage_and_question_types()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var student = await _ctx.CreateStudentAsync();
        await Sut.SubmitExamAsync(student.ApplicationUserId, _course.Id, exam.Id, Answers((1, 2), (2, 2)));

        var result = (await Sut.GetExamResultAsync(student.ApplicationUserId, _course.Id, exam.Id)).Data!;

        Assert.Equal(2, result.Degree);
        Assert.Equal(3, result.TotalDegree);
        Assert.Equal(200.0 / 3, result.Percentage, 6);
        Assert.Equal(1, result.NumberOfMCQQuestions);
        Assert.Equal(1, result.NumberOfTFQuestions);
    }

    [Fact]
    public async Task Result_for_exam_not_taken_is_not_found()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var student = await _ctx.CreateStudentAsync();

        Assert.Equal(ErrorType.NotFound, (await Sut.GetExamResultAsync(student.ApplicationUserId, _course.Id, exam.Id)).ErrorType);
    }

    [Fact]
    public async Task Result_is_only_for_students()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);

        Assert.Equal(ErrorType.Forbidden, (await Sut.GetExamResultAsync(_teacher.ApplicationUserId, _course.Id, exam.Id)).ErrorType);
    }

    [Fact]
    public async Task Update_and_update_time()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);
        var newStart = new DateTime(2027, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        await Sut.UpdateAsync(_course.Id, exam.Id, new UpdateExamDto { Title = "Renamed", Notes = "Bring a pen" });
        await Sut.UpdateExamTimeAsync(_course.Id, exam.Id, new UpdateExamTimeDto { DurationInMinutes = 45, StartAt = newStart });

        var dto = (await Sut.GetByIdAsync(_course.Id, exam.Id)).Data!;
        Assert.Equal("Renamed", dto.Title);
        Assert.Equal("Bring a pen", dto.Notes);
        Assert.Equal(45, dto.DurationInMinutes);
        Assert.Equal(newStart, dto.StartAt);
    }

    [Fact]
    public async Task Update_with_blank_values_changes_nothing()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);

        await Sut.UpdateAsync(_course.Id, exam.Id, new UpdateExamDto { Title = " ", Notes = null });
        await Sut.UpdateExamTimeAsync(_course.Id, exam.Id, new UpdateExamTimeDto());

        var dto = (await Sut.GetByIdAsync(_course.Id, exam.Id)).Data!;
        Assert.Equal("Exam", dto.Title);
        Assert.Equal(60, dto.DurationInMinutes);
    }

    [Fact]
    public async Task Update_delete_missing_exam_is_not_found()
    {
        Assert.Equal(ErrorType.NotFound, (await Sut.UpdateAsync(_course.Id, 7, new UpdateExamDto())).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await Sut.UpdateExamTimeAsync(_course.Id, 7, new UpdateExamTimeDto())).ErrorType);
        Assert.Equal(ErrorType.NotFound, (await Sut.DeleteAsync(_course.Id, 7)).ErrorType);
    }

    [Fact]
    public async Task Delete_removes_exam()
    {
        var exam = await _ctx.CreateExamAsync(_course, _teacher);

        Assert.True((await Sut.DeleteAsync(_course.Id, exam.Id)).IsSuccess);
        Assert.False(await _ctx.Db.Exams.AnyAsync());
    }
}

public class ExamGradingTests
{
    private static List<Question> Questions() =>
    [
        new()
        {
            QuestionNumber = 1, Mark = 2,
            AnswerOptions = { new AnswerOption { OptionNumber = 1 }, new AnswerOption { OptionNumber = 2, IsCorrect = true } },
        },
        new()
        {
            QuestionNumber = 2, Mark = 1.5,
            AnswerOptions = { new AnswerOption { OptionNumber = 1, IsCorrect = true }, new AnswerOption { OptionNumber = 2 } },
        },
    ];

    private static SubmitExamDto A(byte q, byte o) => new() { QuestionNumber = q, OptionNumber = o };

    [Fact]
    public void All_correct_gets_total()
    {
        var (degree, total, answers) = ExamService.Grade(Questions(), [A(1, 2), A(2, 1)]);

        Assert.Equal(3.5, degree);
        Assert.Equal(3.5, total);
        Assert.Equal(2, answers.Count);
    }

    [Fact]
    public void Empty_submission_scores_zero_but_keeps_total()
    {
        var (degree, total, answers) = ExamService.Grade(Questions(), []);

        Assert.Equal(0, degree);
        Assert.Equal(3.5, total);
        Assert.Empty(answers);
    }

    [Fact]
    public void Only_the_last_answer_per_question_counts()
    {
        var (degree, _, answers) = ExamService.Grade(Questions(), [A(1, 2), A(1, 1)]);

        Assert.Equal(0, degree);
        Assert.Equal(1, Assert.Single(answers).OptionNumber);
    }

    [Fact]
    public void Answer_change_to_correct_counts()
    {
        var (degree, _, _) = ExamService.Grade(Questions(), [A(1, 1), A(1, 2)]);

        Assert.Equal(2, degree);
    }

    [Fact]
    public void Answers_to_unknown_questions_are_dropped()
    {
        var (degree, _, answers) = ExamService.Grade(Questions(), [A(9, 1), A(2, 1)]);

        Assert.Equal(1.5, degree);
        Assert.Equal(2, Assert.Single(answers).QuestionNumber);
    }

    [Fact]
    public void Nonexistent_option_scores_nothing()
    {
        var (degree, _, answers) = ExamService.Grade(Questions(), [A(1, 4)]);

        Assert.Equal(0, degree);
        Assert.Single(answers);
    }

    [Fact]
    public void Exam_without_questions_totals_zero()
    {
        var (degree, total, answers) = ExamService.Grade([], [A(1, 1)]);

        Assert.Equal(0, degree);
        Assert.Equal(0, total);
        Assert.Empty(answers);
    }
}
