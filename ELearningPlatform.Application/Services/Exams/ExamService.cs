using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Services.Exams;

public class ExamService(IAppDbContext db, TimeProvider timeProvider) : IExamService
{
    public async Task<Result<ExamDto>> CreateAsync(string userId, int courseId, CreateExamDto dto, CancellationToken cancellationToken = default)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(x => x.ApplicationUserId == userId, cancellationToken);
        if (teacher == null)
            return Result<ExamDto>.Forbidden("Only teachers can create exams");

        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return Result<ExamDto>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        var exam = dto.ToEntity();
        exam.CourseId = courseId;
        exam.TeacherId = teacher.Id;
        db.Exams.Add(exam);
        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(courseId, exam.Id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int courseId, int examId, CancellationToken cancellationToken = default)
    {
        var exam = await FindExamAsync(courseId, examId, cancellationToken);
        if (exam == null)
            return ExamNotFound();

        db.Exams.Remove(exam);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Deleted Successfully");
    }

    public async Task<Result<List<ExamDto>>> GetAllAsync(int courseId, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return Result<List<ExamDto>>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        var examsDto = await db.Exams.Where(x => x.CourseId == courseId).OrderBy(x => x.StartAt)
            .Select(Mappings.ExamToDto).ToListAsync(cancellationToken);
        return Result<List<ExamDto>>.Success(examsDto);
    }

    public async Task<Result<ExamDto>> GetByIdAsync(int courseId, int examId, CancellationToken cancellationToken = default)
    {
        var examDto = await db.Exams.Where(x => x.Id == examId && x.CourseId == courseId)
            .Select(Mappings.ExamToDto).FirstOrDefaultAsync(cancellationToken);
        if (examDto == null)
            return Result<ExamDto>.NotFound("Exam Not Found", ErrorCodes.ExamNotFound);

        return Result<ExamDto>.Success(examDto);
    }

    public async Task<Result<ExamPaperDto>> GetPaperAsync(string userId, int courseId, int examId, CancellationToken cancellationToken = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(x => x.ApplicationUserId == userId, cancellationToken);
        if (student == null)
            return Result<ExamPaperDto>.Forbidden("Only students can take exams");

        var exam = await db.Exams
            .Include(e => e.Questions).ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(x => x.Id == examId && x.CourseId == courseId, cancellationToken);
        if (exam == null)
            return Result<ExamPaperDto>.NotFound("Exam Not Found", ErrorCodes.ExamNotFound);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (now < exam.StartAt)
            return Result<ExamPaperDto>.Validation("Exam Not Started Yet", ErrorCodes.ExamNotStarted);

        if (now > exam.EndAt)
            return Result<ExamPaperDto>.Validation("Exam Ended", ErrorCodes.ExamEnded);

        if (await db.StudentExams.AnyAsync(x => x.ExamId == examId && x.StudentId == student.Id, cancellationToken))
            return Result<ExamPaperDto>.Conflict("You Have Already Submitted This Exam", ErrorCodes.ExamAlreadySubmitted);

        return Result<ExamPaperDto>.Success(new ExamPaperDto
        {
            Id = exam.Id,
            Title = exam.Title,
            Notes = exam.Notes,
            DurationInMinutes = exam.DurationInMinutes,
            StartAt = exam.StartAt,
            EndAt = exam.EndAt,
            Questions = exam.Questions.OrderBy(q => q.QuestionNumber).Select(q => new ExamPaperQuestionDto
            {
                QuestionNumber = q.QuestionNumber,
                Header = q.Header,
                QuestionType = q.QuestionType,
                Mark = q.Mark,
                Options = q.AnswerOptions.OrderBy(o => o.OptionNumber)
                    .Select(o => new ExamPaperOptionDto { OptionNumber = o.OptionNumber, Text = o.Text }).ToList(),
            }).ToList(),
        });
    }

    public async Task<Result> SubmitExamAsync(string userId, int courseId, int examId, List<SubmitExamDto> dto, CancellationToken cancellationToken = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(x => x.ApplicationUserId == userId, cancellationToken);
        if (student == null)
            return Result.Forbidden("Only students can submit exams");

        var exam = await db.Exams
            .Include(e => e.Questions).ThenInclude(q => q.AnswerOptions)
            .FirstOrDefaultAsync(x => x.Id == examId && x.CourseId == courseId, cancellationToken);
        if (exam == null)
            return ExamNotFound();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (now < exam.StartAt)
            return Result.Validation("Exam Not Started Yet", ErrorCodes.ExamNotStarted);

        if (now > exam.EndAt)
            return Result.Validation("Exam Ended", ErrorCodes.ExamEnded);

        if (await db.StudentExams.AnyAsync(x => x.ExamId == examId && x.StudentId == student.Id, cancellationToken))
            return Result.Conflict("You Have Already Submitted This Exam", ErrorCodes.ExamAlreadySubmitted);

        var (degree, totalDegree, answers) = Grade(exam.Questions, dto);

        // StudentExam and its answers are saved in one SaveChanges call, which is already a transaction
        db.StudentExams.Add(new StudentExam
        {
            ExamId = examId,
            StudentId = student.Id,
            SubmittedAt = now,
            Degree = degree,
            TotalDegree = totalDegree,
            StudentAnswers = answers.Select(a => new StudentAnswer
            {
                ExamId = examId,
                StudentId = student.Id,
                QuestionNumber = a.QuestionNumber,
                OptionNumber = a.OptionNumber,
            }).ToList(),
        });
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success($"Exam Submitted Successfully. Score: {degree}/{totalDegree}");
    }

    /// <summary>
    /// Scores a submission. Only the last answer per question counts, and answers to questions
    /// that don't exist are dropped.
    /// </summary>
    internal static (double Degree, double TotalDegree, List<SubmitExamDto> Answers) Grade(
        IEnumerable<Question> examQuestions, IEnumerable<SubmitExamDto> submitted)
    {
        var questions = examQuestions.ToDictionary(q => q.QuestionNumber);
        var answers = submitted
            .GroupBy(a => a.QuestionNumber)
            .Select(g => g.Last())
            .Where(a => questions.ContainsKey(a.QuestionNumber))
            .ToList();

        var degree = answers
            .Where(a => questions[a.QuestionNumber].AnswerOptions.Any(o => o.OptionNumber == a.OptionNumber && o.IsCorrect))
            .Sum(a => questions[a.QuestionNumber].Mark);
        var totalDegree = questions.Values.Sum(q => q.Mark);
        return (degree, totalDegree, answers);
    }

    public async Task<Result<ResultExamDto>> GetExamResultAsync(string userId, int courseId, int examId, CancellationToken cancellationToken = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(x => x.ApplicationUserId == userId, cancellationToken);
        if (student == null)
            return Result<ResultExamDto>.Forbidden("Only students have exam results");

        var studentExam = await db.StudentExams
            .FirstOrDefaultAsync(se => se.ExamId == examId && se.Exam.CourseId == courseId && se.StudentId == student.Id, cancellationToken);
        if (studentExam == null)
            return Result<ResultExamDto>.NotFound("You Did Not Take This Exam", ErrorCodes.ExamNotFound);

        var questionTypes = await db.Questions.Where(x => x.ExamId == examId)
            .GroupBy(x => x.QuestionType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, cancellationToken);

        return Result<ResultExamDto>.Success(new ResultExamDto
        {
            Degree = studentExam.Degree,
            TotalDegree = studentExam.TotalDegree,
            Percentage = studentExam.TotalDegree > 0 ? studentExam.Degree / studentExam.TotalDegree * 100.0 : 0,
            SubmittedAt = studentExam.SubmittedAt ?? DateTime.MinValue,
            NumberOfTFQuestions = questionTypes.GetValueOrDefault(QuestionType.TrueFalse),
            NumberOfMCQQuestions = questionTypes.GetValueOrDefault(QuestionType.MultipleChoice),
        });
    }

    public async Task<Result> UpdateAsync(int courseId, int examId, UpdateExamDto dto, CancellationToken cancellationToken = default)
    {
        var exam = await FindExamAsync(courseId, examId, cancellationToken);
        if (exam == null)
            return ExamNotFound();

        if (!string.IsNullOrWhiteSpace(dto.Title))
            exam.Title = dto.Title;

        if (!string.IsNullOrWhiteSpace(dto.Notes))
            exam.Notes = dto.Notes;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Update Exam Successfully");
    }

    public async Task<Result> UpdateExamTimeAsync(int courseId, int examId, UpdateExamTimeDto dto, CancellationToken cancellationToken = default)
    {
        var exam = await FindExamAsync(courseId, examId, cancellationToken);
        if (exam == null)
            return ExamNotFound();

        if (dto.DurationInMinutes.HasValue)
            exam.DurationInMinutes = dto.DurationInMinutes.Value;

        if (dto.StartAt.HasValue)
            exam.StartAt = dto.StartAt.Value.ToUniversalTime();

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Update Exam Successfully");
    }

    private Task<Exam?> FindExamAsync(int courseId, int examId, CancellationToken cancellationToken)
        => db.Exams.FirstOrDefaultAsync(x => x.Id == examId && x.CourseId == courseId, cancellationToken);

    private static Result ExamNotFound() => Result.NotFound("Exam Not Found", ErrorCodes.ExamNotFound);
}
