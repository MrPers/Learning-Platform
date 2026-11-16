using System.Net;
using ELearningPlatform.API.Tests.Infrastructure;
using ELearningPlatform.Application.Common.Results;
using ELearningPlatform.Application.DTOs.Question;

namespace ELearningPlatform.API.Tests;

public class QuestionsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(TestUser Teacher, int ExamId)> CreateExamAsync()
    {
        var teacher = await factory.CreateUserAsync("Teacher");
        var course = await teacher.CreateCourseAsync();
        var exam = await teacher.CreateExamAsync(course.Id, DateTime.UtcNow.AddHours(1));
        return (teacher, exam.Id);
    }

    private static object Mcq(int correct = 3) => new
    {
        header = "Pick one", mark = 2.5, option1 = "a", option2 = "b", option3 = "c", option4 = "d", correctAnswerIndex = correct,
    };

    [Fact]
    public async Task CreateMcq_Returns201WithFourOptionsAndOneCorrect()
    {
        var (teacher, examId) = await CreateExamAsync();

        var response = await teacher.Client.PostJsonAsync($"api/Exam/{examId}/Questions/MCQ_Question", Mcq(correct: 3));

        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        var question = await response.ReadAsync<QuestionDto>();
        Assert.Equal(1, question.QuestionNumber);
        Assert.Equal(Domain.Enums.QuestionType.MultipleChoice, question.QuestionType);
        Assert.Equal(2.5, question.Mark);
        Assert.Equal([1, 2, 3, 4], question.AnswerOptions.Select(o => (int)o.OptionNumber));
        Assert.Equal(3, question.AnswerOptions.Single(o => o.IsCorrect).OptionNumber);
        Assert.NotNull(response.Headers.Location);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task CreateTrueFalse_MarksTheRightOption(bool isTrue, int correctOption)
    {
        var (teacher, examId) = await CreateExamAsync();

        var response = await teacher.Client.PostJsonAsync($"api/Exam/{examId}/Questions/TF_Question", new { header = "Statement", mark = 1, isTrue });

        var question = await response.ReadAsync<QuestionDto>();
        Assert.Equal(["True", "False"], question.AnswerOptions.Select(o => o.Text));
        Assert.Equal(correctOption, question.AnswerOptions.Single(o => o.IsCorrect).OptionNumber);
    }

    [Fact]
    public async Task QuestionNumbers_AreSequentialPerExam()
    {
        var (teacher, examId) = await CreateExamAsync();

        for (var i = 0; i < 3; i++)
            await teacher.Client.PostJsonAsync($"api/Exam/{examId}/Questions/MCQ_Question", Mcq());
        var all = await (await teacher.Client.GetAsync($"api/Exam/{examId}/Questions")).ReadAsync<List<QuestionDto>>();

        Assert.Equal([1, 2, 3], all.Select(q => (int)q.QuestionNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task CreateMcq_InvalidCorrectIndex_Returns400(int correct)
    {
        var (teacher, examId) = await CreateExamAsync();

        var response = await teacher.Client.PostJsonAsync($"api/Exam/{examId}/Questions/MCQ_Question", Mcq(correct));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CorrectAnswerIndex", await response.ReadValidationErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public async Task Create_InvalidMark_Returns400(double mark)
    {
        var (teacher, examId) = await CreateExamAsync();

        var response = await teacher.Client.PostJsonAsync($"api/Exam/{examId}/Questions/TF_Question", new { header = "S", mark, isTrue = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForUnknownExam_Returns404()
    {
        var teacher = await factory.CreateUserAsync("Teacher");

        var response = await teacher.Client.PostJsonAsync("api/Exam/987654/Questions/MCQ_Question", Mcq());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Exam.NotFound", await response.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task UpdateMark_ThenGet()
    {
        var (teacher, examId) = await CreateExamAsync();
        await teacher.Client.PostJsonAsync($"api/Exam/{examId}/Questions/MCQ_Question", Mcq());

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await teacher.Client.PutJsonAsync($"api/Exam/{examId}/Questions/1", new { mark = 7.5 }));

        Assert.Equal(7.5, (await (await teacher.Client.GetAsync($"api/Exam/{examId}/Questions/1")).ReadAsync<QuestionDto>()).Mark);
    }

    [Fact]
    public async Task Delete_ThenGet_Returns404()
    {
        var (teacher, examId) = await CreateExamAsync();
        await teacher.Client.PostJsonAsync($"api/Exam/{examId}/Questions/MCQ_Question", Mcq());

        await ResponseAssert.StatusAsync(HttpStatusCode.OK, await teacher.Client.DeleteAsync($"api/Exam/{examId}/Questions/1"));

        var get = await teacher.Client.GetAsync($"api/Exam/{examId}/Questions/1");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal("Question.NotFound", await get.ReadProblemCodeAsync());
    }

    [Fact]
    public async Task Pagination_PagesQuestions()
    {
        var (teacher, examId) = await CreateExamAsync();
        for (var i = 0; i < 3; i++)
            await teacher.Client.PostJsonAsync($"api/Exam/{examId}/Questions/MCQ_Question", Mcq());

        var page = await (await teacher.Client.GetAsync($"api/Exam/{examId}/Questions/Pagination?pageNumber=2&pageSize=2"))
            .ReadAsync<PaginatedResult<QuestionDto>>();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(3, Assert.Single(page.Items).QuestionNumber);
        Assert.Null(page.NextPage);
        Assert.NotNull(page.PrevPage);
    }

    [Fact]
    public async Task Student_CannotReadQuestions_BecauseTheyContainAnswers()
    {
        var (_, examId) = await CreateExamAsync();
        var student = await factory.CreateUserAsync("Student");

        Assert.Equal(HttpStatusCode.Forbidden, (await student.Client.GetAsync($"api/Exam/{examId}/Questions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.Client.GetAsync($"api/Exam/{examId}/Questions/1")).StatusCode);
    }
}
