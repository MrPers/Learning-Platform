using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Services.Questions;

public class QuestionService(IAppDbContext db) : IQuestionService
{
    public async Task<Result<QuestionDto>> CreateMcqQuestionAsync(int examId, CreateMcqQuestionDto dto, CancellationToken cancellationToken = default)
    {
        string[] options = [dto.Option1, dto.Option2, dto.Option3, dto.Option4];
        return await CreateQuestionAsync(examId, dto.Header, dto.Mark, QuestionType.MultipleChoice,
            options.Select((text, i) => (text, isCorrect: dto.CorrectAnswerIndex == i + 1)), cancellationToken);
    }

    public async Task<Result<QuestionDto>> CreateTFQuestionAsync(int examId, CreateTFQuestionDto dto, CancellationToken cancellationToken = default)
    {
        return await CreateQuestionAsync(examId, dto.Header, dto.Mark, QuestionType.TrueFalse,
            [("True", dto.IsTrue), ("False", !dto.IsTrue)], cancellationToken);
    }

    public async Task<Result> DeleteQuestionAsync(int examId, byte questionNumber, CancellationToken cancellationToken = default)
    {
        var question = await db.Questions.FindAsync([examId, questionNumber], cancellationToken);
        if (question == null)
            return QuestionNotFound();

        db.Questions.Remove(question);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Question deleted successfully");
    }

    public async Task<Result<List<QuestionDto>>> GetAllQuestionAsync(int examId, CancellationToken cancellationToken = default)
    {
        if (!await db.Exams.AnyAsync(x => x.Id == examId, cancellationToken))
            return Result<List<QuestionDto>>.NotFound("Exam not found", ErrorCodes.ExamNotFound);

        var questionsDto = await db.Questions.Where(x => x.ExamId == examId).OrderBy(x => x.QuestionNumber)
            .Select(Mappings.QuestionToDto).ToListAsync(cancellationToken);
        return Result<List<QuestionDto>>.Success(questionsDto);
    }

    public async Task<Result<PaginatedResult<QuestionDto>>> GetAllQuestionWithPaginationAsync(string baseUrl, int examId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!await db.Exams.AnyAsync(x => x.Id == examId, cancellationToken))
            return Result<PaginatedResult<QuestionDto>>.NotFound("Exam not found", ErrorCodes.ExamNotFound);

        var page = await db.Questions.Where(x => x.ExamId == examId).OrderBy(x => x.QuestionNumber)
            .Select(Mappings.QuestionToDto)
            .ToPaginatedResultAsync(pageNumber, pageSize, baseUrl, cancellationToken);
        return Result<PaginatedResult<QuestionDto>>.Success(page);
    }

    public async Task<Result<QuestionDto>> GetQuestionByIdAsync(int examId, byte questionNumber, CancellationToken cancellationToken = default)
    {
        var questionDto = await db.Questions.Where(x => x.ExamId == examId && x.QuestionNumber == questionNumber)
            .Select(Mappings.QuestionToDto).FirstOrDefaultAsync(cancellationToken);
        if (questionDto == null)
            return Result<QuestionDto>.NotFound("Question not found", ErrorCodes.QuestionNotFound);

        return Result<QuestionDto>.Success(questionDto);
    }

    public async Task<Result> UpdateQuestionDegreeAsync(int examId, byte questionNumber, UpdateQuestionDegreeDto dto, CancellationToken cancellationToken = default)
    {
        var question = await db.Questions.FindAsync([examId, questionNumber], cancellationToken);
        if (question == null)
            return QuestionNotFound();

        question.Mark = dto.Mark;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Question mark updated successfully");
    }

    private async Task<Result<QuestionDto>> CreateQuestionAsync(int examId, string header, double mark, QuestionType type,
        IEnumerable<(string text, bool isCorrect)> options, CancellationToken cancellationToken)
    {
        if (!await db.Exams.AnyAsync(x => x.Id == examId, cancellationToken))
            return Result<QuestionDto>.NotFound("Exam not found", ErrorCodes.ExamNotFound);

        var lastNumber = await db.Questions.Where(x => x.ExamId == examId).MaxAsync(x => (int?)x.QuestionNumber, cancellationToken) ?? 0;
        if (lastNumber >= byte.MaxValue)
            return Result<QuestionDto>.Validation($"An exam can have at most {byte.MaxValue} questions", ErrorCodes.QuestionLimitReached);
        var questionNumber = (byte)(lastNumber + 1);

        db.Questions.Add(new Question
        {
            ExamId = examId,
            QuestionNumber = questionNumber,
            Header = header,
            QuestionType = type,
            Mark = mark,
            AnswerOptions = options.Select((o, i) => new AnswerOption
            {
                ExamId = examId,
                QuestionNumber = questionNumber,
                OptionNumber = (byte)(i + 1),
                Text = o.text,
                IsCorrect = o.isCorrect,
            }).ToList(),
        });
        await db.SaveChangesAsync(cancellationToken);

        return await GetQuestionByIdAsync(examId, questionNumber, cancellationToken);
    }

    private static Result QuestionNotFound() => Result.NotFound("Question not found", ErrorCodes.QuestionNotFound);
}
