using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Infrastructure.Tests.Persistence;

public sealed class AppDbContextTests : IDisposable
{
    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private async Task<(ApplicationUser User, Teacher Teacher)> SeedTeacherAsync(AppDbContext context, string id = "t-user")
    {
        var user = new ApplicationUser { Id = id, UserName = id, Email = $"{id}@x.test", FirstName = "T", LastName = "One" };
        var teacher = new Teacher { User = user, Specialization = Specialization.Physics };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();
        return (user, teacher);
    }

    private static Course NewCourse(Teacher teacher, decimal price = 0) => new()
    {
        Name = "Course",
        Hours = 10,
        Price = price,
        CreateAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Teacher = teacher,
    };

    [Fact]
    public async Task DateTimes_AreReadBackAsUtc()
    {
        await using (var context = _db.CreateContext())
        {
            var (_, teacher) = await SeedTeacherAsync(context);
            context.Courses.Add(NewCourse(teacher));
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var course = await read.Courses.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, course.CreateAt.Kind);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), course.CreateAt);
    }

    [Fact]
    public async Task LocalDateTimes_AreStoredAsUtc()
    {
        var local = new DateTime(2026, 6, 15, 10, 30, 0, DateTimeKind.Local);
        await using (var context = _db.CreateContext())
        {
            var (_, teacher) = await SeedTeacherAsync(context);
            var course = NewCourse(teacher);
            course.CreateAt = local;
            context.Courses.Add(course);
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        Assert.Equal(local.ToUniversalTime(), (await read.Courses.SingleAsync()).CreateAt);
    }

    [Fact]
    public async Task NullableDateTimes_KeepNullAndBecomeUtc()
    {
        await using (var context = _db.CreateContext())
        {
            var (user, _) = await SeedTeacherAsync(context);
            context.RefreshTokens.Add(new RefreshToken { ApplicationUserId = user.Id, TokenHash = "A", CreatedAt = DateTime.UtcNow, ExpiresOn = DateTime.UtcNow.AddDays(1) });
            context.RefreshTokens.Add(new RefreshToken { ApplicationUserId = user.Id, TokenHash = "B", CreatedAt = DateTime.UtcNow, ExpiresOn = DateTime.UtcNow.AddDays(1), RevokedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var tokens = await read.RefreshTokens.OrderBy(t => t.TokenHash).ToListAsync();
        Assert.Null(tokens[0].RevokedAt);
        Assert.Equal(DateTimeKind.Utc, tokens[1].RevokedAt!.Value.Kind);
    }

    [Theory]
    [InlineData("49.99")]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("999999.99")]
    public async Task CoursePrice_DecimalRoundTripsExactly(string value)
    {
        var price = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        await using (var context = _db.CreateContext())
        {
            var (_, teacher) = await SeedTeacherAsync(context);
            context.Courses.Add(NewCourse(teacher, price));
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        Assert.Equal(price, (await read.Courses.SingleAsync()).Price);
    }

    [Fact]
    public async Task CourseIsFree_FollowsPrice()
    {
        await using var context = _db.CreateContext();
        var (_, teacher) = await SeedTeacherAsync(context);
        context.Courses.AddRange(NewCourse(teacher, 0), NewCourse(teacher, 10m));
        await context.SaveChangesAsync();

        var courses = await context.Courses.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.True(courses[0].IsFree);
        Assert.False(courses[1].IsFree);
    }

    [Fact]
    public async Task RefreshTokenHash_MustBeUnique()
    {
        await using var context = _db.CreateContext();
        var (user, _) = await SeedTeacherAsync(context);
        context.RefreshTokens.Add(new RefreshToken { ApplicationUserId = user.Id, TokenHash = "SAME", CreatedAt = DateTime.UtcNow, ExpiresOn = DateTime.UtcNow });
        context.RefreshTokens.Add(new RefreshToken { ApplicationUserId = user.Id, TokenHash = "SAME", CreatedAt = DateTime.UtcNow, ExpiresOn = DateTime.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public void RefreshTokenHash_IsIndexedAndLimitedTo64Chars()
    {
        using var context = _db.CreateContext();
        var entity = context.Model.FindEntityType(typeof(RefreshToken))!;

        var property = entity.FindProperty(nameof(RefreshToken.TokenHash))!;
        Assert.Equal(64, property.GetMaxLength());
        Assert.False(property.IsNullable);
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(RefreshToken.TokenHash));
    }

    [Fact]
    public void RefreshToken_ComputedMembersAreNotMapped()
    {
        using var context = _db.CreateContext();
        var entity = context.Model.FindEntityType(typeof(RefreshToken))!;

        Assert.Null(entity.FindProperty("Token"));
        Assert.Null(entity.FindProperty("IsActive"));
        Assert.Null(entity.FindProperty("IsExpired"));
    }

    [Fact]
    public void CoursePrice_HasPrecision18Scale2()
    {
        using var context = _db.CreateContext();
        var property = context.Model.FindEntityType(typeof(Course))!.FindProperty(nameof(Course.Price))!;

        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }

    [Fact]
    public async Task DeletingUser_CascadesToRefreshTokensAndTeacher()
    {
        await using (var context = _db.CreateContext())
        {
            var (user, _) = await SeedTeacherAsync(context);
            context.RefreshTokens.Add(new RefreshToken { ApplicationUserId = user.Id, TokenHash = "H", CreatedAt = DateTime.UtcNow, ExpiresOn = DateTime.UtcNow });
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            context.Users.Remove(await context.Users.SingleAsync());
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        Assert.Empty(await read.RefreshTokens.ToListAsync());
        Assert.Empty(await read.Teachers.ToListAsync());
    }

    [Fact]
    public async Task DeletingCourseWithExams_IsRestricted()
    {
        await using (var context = _db.CreateContext())
        {
            var (_, teacher) = await SeedTeacherAsync(context);
            var course = NewCourse(teacher);
            context.Exams.Add(new Exam { Title = "E", Course = course, Teacher = teacher, StartAt = DateTime.UtcNow, DurationInMinutes = 30 });
            await context.SaveChangesAsync();
        }

        await using var delete = _db.CreateContext();
        delete.Courses.Remove(await delete.Courses.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => delete.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingExam_CascadesToQuestionsAndOptions()
    {
        await using (var context = _db.CreateContext())
        {
            var (_, teacher) = await SeedTeacherAsync(context);
            var exam = new Exam { Title = "E", Course = NewCourse(teacher), Teacher = teacher, StartAt = DateTime.UtcNow, DurationInMinutes = 30 };
            exam.Questions.Add(new Question
            {
                QuestionNumber = 1,
                Header = "Q",
                QuestionType = QuestionType.TrueFalse,
                Mark = 1,
                AnswerOptions = { new AnswerOption { OptionNumber = 1, Text = "True", IsCorrect = true }, new AnswerOption { OptionNumber = 2, Text = "False" } },
            });
            context.Exams.Add(exam);
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            context.Exams.Remove(await context.Exams.SingleAsync());
            await context.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        Assert.Empty(await read.Questions.ToListAsync());
        Assert.Empty(await read.AnswerOptions.ToListAsync());
    }

    [Fact]
    public async Task ImplementsIAppDbContext()
    {
        await using var context = _db.CreateContext();
        ELearningPlatform.Application.Abstractions.Persistence.IAppDbContext app = context;

        Assert.Same(context.Courses.EntityType, app.Courses.EntityType);
        Assert.Equal(0, await app.SaveChangesAsync());
    }
}
