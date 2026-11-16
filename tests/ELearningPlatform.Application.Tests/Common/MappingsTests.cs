namespace ELearningPlatform.Application.Tests.Common;

/// <summary>The projections compiled and run on in-memory objects (the services' tests cover their SQL translation).</summary>
public class MappingsTests
{
    private static readonly ApplicationUser TeacherUser = new() { Id = "t1", FirstName = "Tess", LastName = "Teach", Email = "t@test.com", Gender = Gender.Female, ProfilePictureUrl = "p.png" };
    private static readonly Teacher Teacher = new() { Id = 1, User = TeacherUser, Specialization = Specialization.Chemistry };

    [Fact]
    public void Course_projection()
    {
        var course = new Course
        {
            Id = 3, Name = "Chem", Description = "d", Hours = 4, Price = 0m, Teacher = Teacher, CreateAt = new DateTime(2026, 1, 1),
            Videos = [new Video(), new Video()],
            CourseComments = [new CourseComment()],
            CourseReactions = [new CourseReaction { IsLike = true }, new CourseReaction { IsLike = false }, new CourseReaction { IsLike = true }],
        };

        var dto = Mappings.CourseToDto.Compile()(course);

        Assert.Equal(3, dto.Id);
        Assert.Equal("Tess Teach", dto.TeacherName);
        Assert.Equal(Specialization.Chemistry, dto.Specialization);
        Assert.True(dto.IsFree);
        Assert.Equal(2, dto.NumberOfVides);
        Assert.Equal(1, dto.NumberOfComment);
        Assert.Equal(2, dto.NumberOfLike);
        Assert.Equal(1, dto.NumberOfDislike);
    }

    [Fact]
    public void Video_projection()
    {
        var video = new Video
        {
            Id = 5, Title = "T", OriginalName = "o.mp4", VideoUrl = "u.mp4", ContentType = "video/mp4",
            Course = new Course { Name = "C", Teacher = Teacher },
            VideoReactions = [new VideoReaction { IsLike = false }],
            VideoComments = [new VideoComment(), new VideoComment()],
        };

        var dto = Mappings.VideoToDto.Compile()(video);

        Assert.Equal("C", dto.CourseName);
        Assert.Equal("Tess Teach", dto.TeacherName);
        Assert.Equal(0, dto.NumberOfLike);
        Assert.Equal(1, dto.NumberOfDislike);
        Assert.Equal(2, dto.NumberOfComment);
    }

    [Fact]
    public void Exam_projection_computes_end_and_marks()
    {
        var exam = new Exam
        {
            Id = 2, Title = "E", StartAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc), DurationInMinutes = 30,
            Course = new Course { Name = "C" }, Teacher = Teacher,
            Questions = [new Question { Mark = 4 }, new Question { Mark = 2 }],
        };

        var dto = Mappings.ExamToDto.Compile()(exam);

        Assert.Equal(exam.StartAt.AddMinutes(30), dto.EndAt);
        Assert.Equal(2, dto.NumberOfQuestions);
        Assert.Equal(6, dto.TotalMark);
        Assert.Equal(3, dto.PassMark);
        Assert.Equal("Tess Teach", dto.TeacherName);
    }

    [Fact]
    public void Question_projection_orders_options()
    {
        var question = new Question
        {
            QuestionNumber = 1, ExamId = 9, Header = "H", Mark = 1, QuestionType = QuestionType.TrueFalse,
            AnswerOptions = [new AnswerOption { OptionNumber = 2, Text = "False" }, new AnswerOption { OptionNumber = 1, Text = "True", IsCorrect = true }],
        };

        var dto = Mappings.QuestionToDto.Compile()(question);

        Assert.Equal(["True", "False"], dto.AnswerOptions.Select(o => o.Text));
        Assert.Equal(9, dto.ExamId);
    }

    [Fact]
    public void User_teacher_student_projections()
    {
        var user = Mappings.UserToDto.Compile()(TeacherUser);
        var teacher = Mappings.TeacherToDto.Compile()(Teacher);
        var student = Mappings.StudentToDto.Compile()(new Student { User = TeacherUser, Level = Level.Intermediate });

        Assert.Equal("t1", user.Id);
        Assert.Equal("p.png", user.ProfilePictureUrl);
        Assert.Equal("t1", teacher.Id);
        Assert.Equal(Specialization.Chemistry, teacher.Specialization);
        Assert.Equal(Level.Intermediate, student.Level);
    }

    [Fact]
    public void User_without_email_maps_to_empty_string()
    {
        Assert.Equal("", Mappings.UserToDto.Compile()(new ApplicationUser()).Email);
    }

    [Fact]
    public void Comment_projections()
    {
        var at = new DateTime(2026, 2, 2);
        var course = Mappings.CourseCommentToDto.Compile()(new CourseComment { Comment = "c", CreatedAt = at, User = TeacherUser });
        var video = Mappings.VideoCommentToDto.Compile()(new VideoComment { Comment = "v", CreatedAt = at, User = TeacherUser });

        Assert.Equal(("Tess Teach", "c", at), (course.UserName, course.Comment, course.CreatedAt));
        Assert.Equal(("Tess Teach", "v", at), (video.UserName, video.Comment, video.CreatedAt));
    }

    [Fact]
    public void Create_dtos_to_entities()
    {
        var course = new CreateCourseDto { Name = "N", Description = "D", Hours = 3, Price = 9.5m }.ToEntity();
        var video = new CreateVideoDto { Title = "T", Description = "D", OriginalName = "o.mp4" }.ToEntity();
        var exam = new CreateExamDto { Title = "E", Notes = "n", DurationInMinutes = 20, StartAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }.ToEntity();

        Assert.Equal(("N", "D", 3, 9.5m), (course.Name, course.Description, course.Hours, course.Price));
        Assert.Equal(("T", "D", "o.mp4"), (video.Title, video.Description, video.OriginalName));
        Assert.Equal(("E", "n", 20), (exam.Title, exam.Notes, exam.DurationInMinutes));
        Assert.Equal(DateTimeKind.Utc, exam.StartAt.Kind);
    }

    [Fact]
    public void Exam_start_in_local_time_is_converted_to_utc()
    {
        var local = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Local);

        var exam = new CreateExamDto { Title = "E", DurationInMinutes = 1, StartAt = local }.ToEntity();

        Assert.Equal(local.ToUniversalTime(), exam.StartAt);
    }

    [Fact]
    public void Course_dto_is_free_only_at_zero()
    {
        Assert.True(new CourseDto { Price = 0m }.IsFree);
        Assert.False(new CourseDto { Price = 0.01m }.IsFree);
    }

    [Fact]
    public void Refresh_token_activity_follows_the_given_time()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var token = new RefreshToken { ExpiresOn = now.AddDays(1) };

        Assert.True(token.IsActive(now));
        Assert.False(token.IsActive(now.AddDays(1)));
        Assert.True(token.IsExpired(now.AddDays(2)));
        token.RevokedAt = now;
        Assert.False(token.IsActive(now));
    }

    [Fact]
    public void Exam_end_is_start_plus_duration()
    {
        var exam = new Exam { StartAt = new DateTime(2026, 1, 1), DurationInMinutes = 90 };

        Assert.Equal(new DateTime(2026, 1, 1, 1, 30, 0), exam.EndAt);
    }
}
