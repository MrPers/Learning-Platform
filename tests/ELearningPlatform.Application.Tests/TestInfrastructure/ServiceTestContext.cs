using ELearningPlatform.Application;
using ELearningPlatform.Application.Abstractions.Persistence;
using ELearningPlatform.Application.Abstractions.Services;
using ELearningPlatform.Domain.Entities;
using ELearningPlatform.Domain.Enums;
using ELearningPlatform.Infrastructure;
using ELearningPlatform.Infrastructure.Persistence;
using ELearningPlatform.Shared.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ELearningPlatform.Application.Tests.TestInfrastructure;

/// <summary>
/// Real application services wired exactly like production (AddApplication + AddInfrastructure) on a
/// private in-memory SQLite database, with external side effects (email, SMS, files, Google) substituted.
/// </summary>
public sealed class ServiceTestContext : IAsyncDisposable
{
    public const string Password = "Passw0rd!";
    public static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _keepAlive;
    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    public FakeTimeProvider Time { get; } = new(Start);
    public IAppEmailService Email { get; } = Substitute.For<IAppEmailService>();
    public INotificationService Notifications { get; } = Substitute.For<INotificationService>();
    public IMediaService Media { get; } = Substitute.For<IMediaService>();
    public IGoogleTokenValidator Google { get; } = Substitute.For<IGoogleTokenValidator>();
    public IPaymentGateway Payments { get; } = Substitute.For<IPaymentGateway>();
    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public IServiceProvider Services => _scope.ServiceProvider;
    public AppDbContext Db => Get<AppDbContext>();
    public UserManager<ApplicationUser> Users => Get<UserManager<ApplicationUser>>();
    public DateTime Now => Time.GetUtcNow().UtcDateTime;

    private ServiceTestContext()
    {
        var connectionString = $"Data Source=file:apptests-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(connectionString);
        _keepAlive.Open();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(TestConfiguration.Values(connectionString)).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<TimeProvider>(Time);
        services.AddApplication();
        services.AddInfrastructure();

        services.Replace(ServiceDescriptor.Singleton(Email));
        services.Replace(ServiceDescriptor.Singleton(Notifications));
        services.Replace(ServiceDescriptor.Singleton(Media));
        services.Replace(ServiceDescriptor.Singleton(Google));
        services.Replace(ServiceDescriptor.Singleton(Payments));
        services.Replace(ServiceDescriptor.Singleton(CurrentUser));

        _root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _root.CreateAsyncScope();
    }

    public static async Task<ServiceTestContext> CreateAsync()
    {
        var context = new ServiceTestContext();
        await context.Db.Database.EnsureCreatedAsync();

        var roles = context.Get<RoleManager<IdentityRole>>();
        foreach (var role in new[] { RolesConstants.Admin, RolesConstants.Teacher, RolesConstants.Student })
            await roles.CreateAsync(new IdentityRole(role));
        return context;
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>A fresh scope (new DbContext) to read what was really persisted, bypassing the change tracker.</summary>
    public async Task<TResult> InNewScopeAsync<TResult>(Func<IServiceProvider, Task<TResult>> action)
    {
        await using var scope = _root.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<ApplicationUser> CreateUserAsync(string role, string? email = null, bool emailConfirmed = true,
        string password = Password, string firstName = "Test", string lastName = "User", string? phone = "+201012345678")
    {
        var user = new ApplicationUser
        {
            UserName = Guid.NewGuid().ToString(),
            Email = email ?? $"{Guid.NewGuid():N}@test.com",
            EmailConfirmed = emailConfirmed,
            FirstName = firstName,
            LastName = lastName,
            Gender = Gender.Female,
            PhoneNumber = phone,
        };
        var result = await Users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        await Users.AddToRoleAsync(user, role);
        return user;
    }

    public async Task<Teacher> CreateTeacherAsync(Specialization specialization = Specialization.ComputerScience, string firstName = "Tom", string lastName = "Teacher")
    {
        var user = await CreateUserAsync(RolesConstants.Teacher, firstName: firstName, lastName: lastName);
        var teacher = new Teacher { ApplicationUserId = user.Id, Specialization = specialization };
        Db.Teachers.Add(teacher);
        await Db.SaveChangesAsync();
        return teacher;
    }

    public async Task<Student> CreateStudentAsync(Level level = Level.Beginner)
    {
        var user = await CreateUserAsync(RolesConstants.Student, firstName: "Sam", lastName: "Student");
        var student = new Student { ApplicationUserId = user.Id, Level = level };
        Db.Students.Add(student);
        await Db.SaveChangesAsync();
        return student;
    }

    public async Task<Course> CreateCourseAsync(Teacher teacher, string name = "Course", decimal price = 10m, int hours = 5)
    {
        var course = new Course { Name = name, Description = "Desc", Hours = hours, Price = price, TeacherId = teacher.Id, CreateAt = Now };
        Db.Courses.Add(course);
        await Db.SaveChangesAsync();
        return course;
    }

    public async Task<Video> CreateVideoAsync(Course course, string title = "Video", string originalName = "video.mp4")
    {
        var video = new Video
        {
            CourseId = course.Id,
            Title = title,
            OriginalName = originalName,
            VideoUrl = $"{Guid.NewGuid():N}.mp4",
            ContentType = "video/mp4",
            UploadedAt = Now,
        };
        Db.Videos.Add(video);
        await Db.SaveChangesAsync();
        return video;
    }

    /// <summary>Exam with an MCQ (correct option 2, mark 2) and a true/false question (correct "True", mark 1).</summary>
    public async Task<Exam> CreateExamAsync(Course course, Teacher teacher, DateTime? startAt = null, int durationInMinutes = 60, bool withQuestions = true)
    {
        var exam = new Exam
        {
            Title = "Exam",
            CourseId = course.Id,
            TeacherId = teacher.Id,
            StartAt = startAt ?? Now.AddMinutes(-5),
            DurationInMinutes = durationInMinutes,
        };
        if (withQuestions)
        {
            exam.Questions.Add(new Question
            {
                QuestionNumber = 1, Header = "MCQ", QuestionType = QuestionType.MultipleChoice, Mark = 2,
                AnswerOptions =
                {
                    new AnswerOption { OptionNumber = 1, Text = "a" },
                    new AnswerOption { OptionNumber = 2, Text = "b", IsCorrect = true },
                    new AnswerOption { OptionNumber = 3, Text = "c" },
                    new AnswerOption { OptionNumber = 4, Text = "d" },
                },
            });
            exam.Questions.Add(new Question
            {
                QuestionNumber = 2, Header = "TF", QuestionType = QuestionType.TrueFalse, Mark = 1,
                AnswerOptions =
                {
                    new AnswerOption { OptionNumber = 1, Text = "True", IsCorrect = true },
                    new AnswerOption { OptionNumber = 2, Text = "False" },
                },
            });
        }
        Db.Exams.Add(exam);
        await Db.SaveChangesAsync();
        return exam;
    }

    /// <summary>Credits a user's wallet the way a succeeded Stripe top-up does (ledger entry from external clearing).</summary>
    public async Task<Wallet> FundWalletAsync(string userId, decimal amount)
    {
        var ledger = Get<ELearningPlatform.Application.Services.Payments.Ledger>();
        var result = await Get<ELearningPlatform.Application.Services.Payments.FinanceTransaction>().ExecuteAsync(async ct =>
        {
            var wallet = await ledger.GetOrCreateUserWalletAsync(userId, ct);
            var clearing = await ledger.GetSystemWalletAsync(WalletType.ExternalClearing, ct);
            ledger.Post(LedgerTransactionType.TopUp, "Test funding", null, userId,
            [
                new(clearing, EntryDirection.Debit, amount),
                new(wallet, EntryDirection.Credit, amount),
            ]);
            return Result<Wallet>.Success(wallet);
        });
        Assert.True(result.IsSuccess, result.Message);
        return result.Data!;
    }

    /// <summary>Balance as stored in the database (fresh context, bypassing the change tracker).</summary>
    public Task<decimal> BalanceAsync(string userId) => InNewScopeAsync(async sp =>
        (await sp.GetRequiredService<AppDbContext>().Wallets.Where(w => w.OwnerUserId == userId).Select(w => (decimal?)w.Balance).FirstOrDefaultAsync()) ?? 0m);

    public Task<decimal> SystemBalanceAsync(WalletType type) => InNewScopeAsync(async sp =>
        (await sp.GetRequiredService<AppDbContext>().Wallets.Where(w => w.Type == type).Select(w => (decimal?)w.Balance).FirstOrDefaultAsync()) ?? 0m);

    /// <summary>Asserts the ledger invariants: balanced transactions, cached balances = entries, total = 0.</summary>
    public async Task AssertLedgerConsistentAsync()
    {
        var report = await InNewScopeAsync(sp => sp.GetRequiredService<ELearningPlatform.Application.Services.Payments.IWalletService>().ReconcileAsync());
        Assert.True(report.Data!.IsConsistent, $"Ledger inconsistent: total {report.Data.LedgerTotal}, {report.Data.Mismatches.Count} mismatches, {report.Data.UnbalancedTransactions.Count} unbalanced");
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }
}
