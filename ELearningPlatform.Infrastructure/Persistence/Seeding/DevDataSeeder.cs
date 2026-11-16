using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Infrastructure.Persistence.Seeding;

// Test data for local development only. Runs once: skipped if any course already exists.
// All seeded teachers/students share the password from Seed:DevUsersPassword.
public static class DevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = serviceProvider.GetRequiredService<AppDbContext>();
        var password = serviceProvider.GetRequiredService<IOptions<SeedSettings>>().Value.DevUsersPassword;
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Seed:DevUsersPassword must be set to seed development data.");

        if (await db.Courses.AnyAsync())
            return;

        // 1- Teachers
        var teachers = new List<Teacher>();
        foreach (var (first, last, gender, spec) in new[]
        {
            ("Ivan", "Petrov", Gender.Male, Specialization.ComputerScience),
            ("Olga", "Smirnova", Gender.Female, Specialization.Mathematics),
            ("Sergey", "Volkov", Gender.Male, Specialization.Physics),
        })
        {
            var user = await CreateUserAsync(userManager, password, first, last, gender, RolesConstants.Teacher);
            teachers.Add(new Teacher { ApplicationUserId = user.Id, Specialization = spec });
        }
        db.Teachers.AddRange(teachers);

        // 2- Students
        var students = new List<Student>();
        foreach (var (first, last, gender, level) in new[]
        {
            ("Anna", "Ivanova", Gender.Female, Level.Beginner),
            ("Dmitry", "Kozlov", Gender.Male, Level.Intermediate),
            ("Maria", "Sokolova", Gender.Female, Level.Advanced),
            ("Alexey", "Morozov", Gender.Male, Level.Beginner),
            ("Elena", "Popova", Gender.Female, Level.Expert),
        })
        {
            var user = await CreateUserAsync(userManager, password, first, last, gender, RolesConstants.Student);
            students.Add(new Student { ApplicationUserId = user.Id, Level = level });
        }
        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // 3- Courses with videos
        var now = serviceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var courses = new List<Course>
        {
            NewCourse("C# for Beginners", "Syntax, types, OOP and LINQ basics.", 24, 0, teachers[0], now.AddDays(-60),
                "Introduction to .NET", "Variables and Types", "Classes and Objects", "LINQ Basics"),
            NewCourse("ASP.NET Core Web API", "Build REST APIs with EF Core, JWT and Swagger.", 40, 49.99m, teachers[0], now.AddDays(-30),
                "Project Setup", "Controllers and Routing", "EF Core and Migrations", "JWT Authentication"),
            NewCourse("Linear Algebra", "Vectors, matrices, eigenvalues.", 30, 29.99m, teachers[1], now.AddDays(-45),
                "Vectors", "Matrix Operations", "Determinants"),
            NewCourse("Calculus I", "Limits, derivatives and integrals.", 36, 0, teachers[1], now.AddDays(-10),
                "Limits", "Derivatives", "Integrals"),
            NewCourse("Classical Mechanics", "Newton's laws, energy and momentum.", 28, 19.99m, teachers[2], now.AddDays(-5),
                "Newton's Laws", "Work and Energy", "Momentum"),
        };
        db.Courses.AddRange(courses);
        await db.SaveChangesAsync();

        // Placeholder files so the download endpoint has something to return
        var env = serviceProvider.GetRequiredService<IWebHostEnvironment>();
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var videosFolder = Path.Combine(webRoot, FolderConstants.UploadsFolder, FolderConstants.VideosFolder);
        Directory.CreateDirectory(videosFolder);
        foreach (var video in courses.SelectMany(c => c.Videos))
            await File.WriteAllTextAsync(Path.Combine(videosFolder, video.VideoUrl), $"Placeholder for \"{video.Title}\"");

        // 4- Enrollments
        var enrollments = new (int student, int course)[]
        {
            (0, 0), (0, 3), (1, 0), (1, 1), (2, 1), (2, 2), (3, 0), (3, 4), (4, 1), (4, 2), (4, 3),
        };
        foreach (var (s, c) in enrollments)
            db.Set<StudentCourse>().Add(new StudentCourse { StudentId = students[s].Id, CourseId = courses[c].Id, EnrolledDate = now.AddDays(-3) });

        // 5- Course reactions (likes + ratings) and comments
        var rnd = new Random(42);
        foreach (var (s, c) in enrollments)
        {
            db.Set<CourseReaction>().Add(new CourseReaction
            {
                CourseId = courses[c].Id,
                UserId = students[s].ApplicationUserId,
                IsLike = rnd.Next(5) > 0,
                Rating = (Rating)rnd.Next(3, 6),
                ReactedAt = now.AddDays(-2),
            });
            db.Set<CourseComment>().Add(new CourseComment
            {
                CourseId = courses[c].Id,
                UserId = students[s].ApplicationUserId,
                Comment = new[] { "Great course!", "Very clear explanations.", "Would like more examples.", "Helpful, thanks!" }[rnd.Next(4)],
                CreatedAt = now.AddDays(-2),
            });
        }

        // 6- Video reactions and comments
        foreach (var video in courses.SelectMany(c => c.Videos))
        {
            foreach (var student in students.Where(_ => rnd.Next(2) == 0))
            {
                db.Set<VideoReaction>().Add(new VideoReaction { VideoId = video.Id, UserId = student.ApplicationUserId, IsLike = rnd.Next(4) > 0, ReactedAt = now.AddDays(-1) });
            }
            db.Set<VideoComment>().Add(new VideoComment
            {
                VideoId = video.Id,
                UserId = students[rnd.Next(students.Count)].ApplicationUserId,
                Comment = "Thanks for the video!",
                CreatedAt = now.AddDays(-1),
            });
        }
        await db.SaveChangesAsync();

        // 7- Exams: one finished (with a submitted result), one open now, one upcoming
        var finishedExam = NewExam("C# Basics Quiz", courses[0], teachers[0], now.AddDays(-2), 30);
        var openExam = NewExam("Web API Midterm", courses[1], teachers[0], now.AddMinutes(-10), 24 * 60);
        var upcomingExam = NewExam("Linear Algebra Final", courses[2], teachers[1], now.AddDays(7), 60);
        db.Exams.AddRange(finishedExam, openExam, upcomingExam);
        await db.SaveChangesAsync();

        // Student 0 submitted the finished exam: correct on Q1, wrong on Q2
        var studentExam = new StudentExam
        {
            StudentId = students[0].Id,
            ExamId = finishedExam.Id,
            TotalDegree = finishedExam.Questions.Sum(q => q.Mark),
            Degree = finishedExam.Questions.First(q => q.QuestionNumber == 1).Mark,
            SubmittedAt = now.AddDays(-2).AddMinutes(20),
            StudentAnswers =
            {
                new StudentAnswer { ExamId = finishedExam.Id, StudentId = students[0].Id, QuestionNumber = 1, OptionNumber = 2 },
                new StudentAnswer { ExamId = finishedExam.Id, StudentId = students[0].Id, QuestionNumber = 2, OptionNumber = 2 },
            },
        };
        db.Set<StudentExam>().Add(studentExam);
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string password,
        string firstName, string lastName, Gender gender, string role)
    {
        var user = new ApplicationUser
        {
            UserName = $"{firstName}.{lastName}".ToLower(),
            Email = $"{firstName}.{lastName}@elearning.com".ToLower(),
            FirstName = firstName,
            LastName = lastName,
            Gender = gender,
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static Course NewCourse(string name, string description, int hours, decimal price, Teacher teacher,
        DateTime createdAt, params string[] videoTitles)
    {
        var course = new Course
        {
            Name = name,
            Description = description,
            Hours = hours,
            Price = price,
            Teacher = teacher,
            CreateAt = createdAt,
        };
        for (int i = 0; i < videoTitles.Length; i++)
        {
            course.Videos.Add(new Video
            {
                Title = videoTitles[i],
                Description = $"Lesson {i + 1} of {name}",
                OriginalName = $"lesson{i + 1}.mp4",
                VideoUrl = $"sample-{Guid.NewGuid():N}.mp4",
                ContentType = "video/mp4",
                UploadedAt = createdAt.AddDays(i),
            });
        }
        return course;
    }

    private static Exam NewExam(string title, Course course, Teacher teacher, DateTime startAt, int durationInMinutes)
    {
        var exam = new Exam
        {
            Title = title,
            Notes = "Answer all questions.",
            DurationInMinutes = durationInMinutes,
            StartAt = startAt,
            Course = course,
            Teacher = teacher,
        };
        exam.Questions.Add(Mcq(1, "Which keyword declares a constant in C#?", 2, "var", "const", "static", "readonly"));
        exam.Questions.Add(TrueFalse(2, "A class can inherit from multiple classes in C#.", correct: false));
        exam.Questions.Add(Mcq(3, "Which HTTP method is idempotent?", 3, "POST", "PATCH", "PUT", "CONNECT"));
        return exam;
    }

    private static Question Mcq(byte number, string header, byte correctOption, params string[] options)
    {
        var question = new Question { QuestionNumber = number, Header = header, QuestionType = QuestionType.MultipleChoice, Mark = 2 };
        for (byte i = 0; i < options.Length; i++)
            question.AnswerOptions.Add(new AnswerOption { OptionNumber = (byte)(i + 1), Text = options[i], IsCorrect = i + 1 == correctOption });
        return question;
    }

    private static Question TrueFalse(byte number, string header, bool correct) => new()
    {
        QuestionNumber = number,
        Header = header,
        QuestionType = QuestionType.TrueFalse,
        Mark = 1,
        AnswerOptions =
        {
            new AnswerOption { OptionNumber = 1, Text = "True", IsCorrect = correct },
            new AnswerOption { OptionNumber = 2, Text = "False", IsCorrect = !correct },
        },
    };
}
