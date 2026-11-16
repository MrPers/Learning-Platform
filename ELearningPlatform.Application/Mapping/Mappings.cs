using System.Linq.Expressions;

namespace ELearningPlatform.Application.Mapping;

/// <summary>
/// Hand-written mappings. Projections are expression trees, so EF Core translates them to SQL
/// and only the columns a DTO needs are read; the compiled versions map objects already in memory.
/// </summary>
public static class Mappings
{
    public static readonly Expression<Func<Course, CourseDto>> CourseToDto = c => new CourseDto
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        Hours = c.Hours,
        CreateAt = c.CreateAt,
        Price = c.Price,
        TeacherName = c.Teacher.User.FirstName + " " + c.Teacher.User.LastName,
        Specialization = c.Teacher.Specialization,
        NumberOfVides = c.Videos.Count,
        NumberOfComment = c.CourseComments.Count,
        NumberOfLike = c.CourseReactions.Count(r => r.IsLike),
        NumberOfDislike = c.CourseReactions.Count(r => !r.IsLike),
    };

    public static readonly Expression<Func<Video, VideoDto>> VideoToDto = v => new VideoDto
    {
        Id = v.Id,
        Title = v.Title,
        Description = v.Description,
        OriginalName = v.OriginalName,
        VideoUrl = v.VideoUrl,
        ContentType = v.ContentType,
        UploadedAt = v.UploadedAt,
        CourseName = v.Course.Name,
        TeacherName = v.Course.Teacher.User.FirstName + " " + v.Course.Teacher.User.LastName,
        NumberOfComment = v.VideoComments.Count,
        NumberOfLike = v.VideoReactions.Count(r => r.IsLike),
        NumberOfDislike = v.VideoReactions.Count(r => !r.IsLike),
    };

    public static readonly Expression<Func<Exam, ExamDto>> ExamToDto = e => new ExamDto
    {
        Id = e.Id,
        Title = e.Title,
        Notes = e.Notes,
        DurationInMinutes = e.DurationInMinutes,
        StartAt = e.StartAt,
        EndAt = e.StartAt.AddMinutes(e.DurationInMinutes),
        NumberOfQuestions = e.Questions.Count,
        TotalMark = e.Questions.Sum(q => q.Mark),
        PassMark = e.Questions.Sum(q => q.Mark) / 2,
        CourseName = e.Course.Name,
        TeacherName = e.Teacher.User.FirstName + " " + e.Teacher.User.LastName,
    };

    public static readonly Expression<Func<Question, QuestionDto>> QuestionToDto = q => new QuestionDto
    {
        QuestionNumber = q.QuestionNumber,
        Header = q.Header,
        QuestionType = q.QuestionType,
        Mark = q.Mark,
        ExamId = q.ExamId,
        AnswerOptions = q.AnswerOptions
            .OrderBy(o => o.OptionNumber)
            .Select(o => new AnswerOptionDto
            {
                QuestionNumber = o.QuestionNumber,
                OptionNumber = o.OptionNumber,
                Text = o.Text,
                IsCorrect = o.IsCorrect,
            })
            .ToList(),
    };

    public static readonly Expression<Func<ApplicationUser, UserDto>> UserToDto = u => new UserDto
    {
        Id = u.Id,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email ?? "",
        Gender = u.Gender,
        ProfilePictureUrl = u.ProfilePictureUrl,
    };

    public static readonly Expression<Func<Teacher, TeacherDto>> TeacherToDto = t => new TeacherDto
    {
        Id = t.User.Id,
        FirstName = t.User.FirstName,
        LastName = t.User.LastName,
        Email = t.User.Email ?? "",
        Gender = t.User.Gender,
        ProfilePictureUrl = t.User.ProfilePictureUrl,
        Specialization = t.Specialization,
    };

    public static readonly Expression<Func<Student, StudentDto>> StudentToDto = s => new StudentDto
    {
        Id = s.User.Id,
        FirstName = s.User.FirstName,
        LastName = s.User.LastName,
        Email = s.User.Email ?? "",
        Gender = s.User.Gender,
        ProfilePictureUrl = s.User.ProfilePictureUrl,
        Level = s.Level,
    };

    public static readonly Expression<Func<CourseComment, CommentDto>> CourseCommentToDto = c => new CommentDto
    {
        UserName = c.User.FirstName + " " + c.User.LastName,
        Comment = c.Comment,
        CreatedAt = c.CreatedAt,
    };

    public static readonly Expression<Func<VideoComment, CommentDto>> VideoCommentToDto = c => new CommentDto
    {
        UserName = c.User.FirstName + " " + c.User.LastName,
        Comment = c.Comment,
        CreatedAt = c.CreatedAt,
    };

    public static Course ToEntity(this CreateCourseDto dto) => new()
    {
        Name = dto.Name,
        Description = dto.Description,
        Hours = dto.Hours,
        Price = dto.Price,
    };

    public static Video ToEntity(this CreateVideoDto dto) => new()
    {
        Title = dto.Title,
        Description = dto.Description,
        OriginalName = dto.OriginalName,
    };

    public static Exam ToEntity(this CreateExamDto dto) => new()
    {
        Title = dto.Title,
        Notes = dto.Notes,
        DurationInMinutes = dto.DurationInMinutes,
        StartAt = dto.StartAt.ToUniversalTime(),
    };
}
