using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.DTOs.Exam;

namespace ELearningPlatform.API.Tests;

public class ExamsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// Exams can only be created with a future start, so the "open exam" starts a moment from now
    /// and the test waits until it has begun.
    /// </summary>
    private async Task<(TestUser Teacher, int CourseId, ExamDto Exam)> CreateOpenExamWithQuestionsAsync()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var startAt = DateTime.UtcNow.AddSeconds(1);
        var exam = await teacher.CreateExamAsync(course.Id, startAt);

        // Q1: MCQ worth 2, option 2 correct. Q2: true/false worth 1, answer "False" (option 2)
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, await teacher.Client.PostJsonAsync($"api/Exam/{exam.Id}/Questions/MCQ_Question",
            new { header = "2 + 2 = ?", mark = 2, option1 = "3", option2 = "4", option3 = "5", option4 = "22", correctAnswerIndex = 2 }));
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, await teacher.Client.PostJsonAsync($"api/Exam/{exam.Id}/Questions/TF_Question",
            new { header = "The sun is cold", mark = 1, isTrue = false }));

        var wait = startAt - DateTime.UtcNow + TimeSpan.FromMilliseconds(200);
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait);
        return (teacher, course.Id, exam);
    }

    [Fact]
    public async Task Create_AsTeacher_Returns201WithComputedFields()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync(name: "Exam course");
        var startAt = DateTime.UtcNow.AddHours(2);

        var response = await teacher.Client.PostJsonAsync($"api/courses/{course.Id}/Exams",
            new { title = "Final", durationInMinutes = 90, startAt });

        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        var exam = await response.ReadAsync<ExamDto>();
        Assert.Equal("Final", exam.Title);
        Assert.Equal("Exam course", exam.CourseName);
        Assert.Equal(0, exam.NumberOfQuestions);
        Assert.Equal(90, (exam.EndAt - exam.StartAt).TotalMinutes, precision: 3);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task ExamDto_TotalMarkAndQuestionCount_ReflectQuestions()
    {
        var (teacher, courseId, exam) = await CreateOpenExamWithQuestionsAsync();

        var dto = await (await teacher.Client.GetAsync($"api/courses/{courseId}/Exams/{exam.Id}")).ReadAsync<ExamDto>();

        Assert.Equal(2, dto.NumberOfQuestions);
        Assert.Equal(3, dto.TotalMark);
        Assert.Equal(1.5, dto.PassMark);
    }

    [Theory]
    [InlineData(-60, 60)]
    [InlineData(60, 0)]
    [InlineData(60, 10081)]
    public async Task Create_InvalidStartOrDuration_Returns400(int startOffsetMinutes, int duration)
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();

        var response = await teacher.Client.PostJsonAsync($"api/courses/{course.Id}/Exams",
            new { title = "Bad", durationInMinutes = duration, startAt = DateTime.UtcNow.AddMinutes(startOffsetMinutes) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForUnknownCourse_Returns404()
    {
        var teacher = await factory.CreateUserAsync("Teacher");

        var response = await teacher.Client.PostJsonAsync("api/courses/987654/Exams",
            new { title = "x", durationInMinutes = 10, startAt = DateTime.UtcNow.AddHours(1) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Course.NotFound", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Create_AsStudent_Returns403()
    {
        var student = await factory.CreateUserAsync("Student");

        var response = await student.Client.PostJsonAsync("api/courses/1/Exams",
            new { title = "x", durationInMinutes = 10, startAt = DateTime.UtcNow.AddHours(1) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Submit_AllCorrect_ScoresFullMarks_AndResultIsAvailable()
    {
        var (_, courseId, exam) = await CreateOpenExamWithQuestionsAsync();
        var student = await factory.CreateUserAsync("Student");

        var submit = await student.Client.PostJsonAsync($"api/courses/{courseId}/Exams/{exam.Id}/Submit",
            new[] { new { questionNumber = 1, optionNumber = 2 }, new { questionNumber = 2, optionNumber = 2 } });

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, submit);
        Assert.Contains("3/3", (await submit.ReadJsonAsync()).GetProperty("message").GetString());

        var result = await (await student.Client.GetAsync($"api/courses/{courseId}/Exams/{exam.Id}/Result")).ReadAsync<ResultExamDto>();
        Assert.Equal(3, result.Degree);
        Assert.Equal(3, result.TotalDegree);
        Assert.Equal(100, result.Percentage);
        Assert.Equal(1, result.NumberOfMCQQuestions);
        Assert.Equal(1, result.NumberOfTFQuestions);
    }

    [Fact]
    public async Task Submit_PartlyWrong_ScoresOnlyCorrectAnswers()
    {
        var (_, courseId, exam) = await CreateOpenExamWithQuestionsAsync();
        var student = await factory.CreateUserAsync("Student");

        await student.Client.PostJsonAsync($"api/courses/{courseId}/Exams/{exam.Id}/Submit",
            new[] { new { questionNumber = 1, optionNumber = 1 }, new { questionNumber = 2, optionNumber = 2 } });

        var result = await (await student.Client.GetAsync($"api/courses/{courseId}/Exams/{exam.Id}/Result")).ReadAsync<ResultExamDto>();
        Assert.Equal(1, result.Degree);
        Assert.Equal(100.0 / 3, result.Percentage, precision: 6);
    }

    [Fact]
    public async Task Submit_Twice_Returns409()
    {
        var (_, courseId, exam) = await CreateOpenExamWithQuestionsAsync();
        var student = await factory.CreateUserAsync("Student");
        var answers = new[] { new { questionNumber = 1, optionNumber = 2 } };

        await student.Client.PostJsonAsync($"api/courses/{courseId}/Exams/{exam.Id}/Submit", answers);
        var second = await student.Client.PostJsonAsync($"api/courses/{courseId}/Exams/{exam.Id}/Submit", answers);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("Exam.AlreadySubmitted", await second.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Submit_BeforeStart_Returns400NotStarted()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var exam = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddHours(3));
        var student = await factory.CreateUserAsync("Student");

        var response = await student.Client.PostJsonAsync($"api/courses/{course.Id}/Exams/{exam.Id}/Submit",
            new[] { new { questionNumber = 1, optionNumber = 1 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Exam.NotStarted", await response.ReadProblemCodeAsync());
    }

    [Theory]
    [InlineData(0, 1, "answers[0].QuestionNumber")]
    [InlineData(101, 1, "answers[0].QuestionNumber")]
    [InlineData(1, 5, "answers[0].OptionNumber")]
    [InlineData(1, 0, "answers[0].OptionNumber")]
    public async Task Submit_InvalidItems_Returns400WithIndexedKeys(int questionNumber, int optionNumber, string expectedKey)
    {
        var student = await factory.CreateUserAsync("Student");

        var response = await student.Client.PostJsonAsync("api/courses/1/Exams/1/Submit",
            new[] { new { questionNumber, optionNumber } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedKey, await response.ReadValidationErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_AsTeacher_Returns403()
    {
        var teacher = await factory.CreateUserAsync("Teacher");

        var response = await teacher.Client.PostJsonAsync("api/courses/1/Exams/1/Submit", new[] { new { questionNumber = 1, optionNumber = 1 } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Result_WithoutSubmission_Returns404()
    {
        var (_, courseId, exam) = await CreateOpenExamWithQuestionsAsync();
        var student = await factory.CreateUserAsync("Student");

        var response = await student.Client.GetAsync($"api/courses/{courseId}/Exams/{exam.Id}/Result");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_TitleAndTime()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var exam = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddHours(1));

        await ResponseAssert.StatusAsync(HttpStatusCode.OK,
            await teacher.Client.PutJsonAsync($"api/courses/{course.Id}/Exams/{exam.Id}", new { title = "Renamed" }));
        await ResponseAssert.StatusAsync(HttpStatusCode.OK,
            await teacher.Client.PatchJsonAsync($"api/courses/{course.Id}/Exams/{exam.Id}", new { durationInMinutes = 30 }));

        var dto = await (await teacher.Client.GetAsync($"api/courses/{course.Id}/Exams/{exam.Id}")).ReadAsync<ExamDto>();
        Assert.Equal("Renamed", dto.Title);
        Assert.Equal(30, dto.DurationInMinutes);
    }

    [Fact]
    public async Task UpdateTime_StartInPast_Returns400()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var exam = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddHours(1));

        var response = await teacher.Client.PatchJsonAsync($"api/courses/{course.Id}/Exams/{exam.Id}", new { startAt = DateTime.UtcNow.AddDays(-1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGet_Returns404()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var exam = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddHours(1));

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await teacher.Client.DeleteAsync($"api/courses/{course.Id}/Exams/{exam.Id}"));

        var get = await teacher.Client.GetAsync($"api/courses/{course.Id}/Exams/{exam.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal("Exam.NotFound", await get.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task GetAll_ListsExamsOfCourseOrderedByStart()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var later = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddDays(2));
        var sooner = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddDays(1));

        var exams = await (await teacher.Client.GetAsync($"api/courses/{course.Id}/Exams")).ReadAsync<List<ExamDto>>();

        Assert.Equal([sooner.Id, later.Id], exams.Select(e => e.Id));
    }

    [Fact]
    public async Task GetExam_ThroughOtherCourse_Returns404()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var other = await teacher.CreateCourseAsync();
        var exam = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddHours(1));

        var response = await teacher.Client.GetAsync($"api/courses/{other.Id}/Exams/{exam.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
