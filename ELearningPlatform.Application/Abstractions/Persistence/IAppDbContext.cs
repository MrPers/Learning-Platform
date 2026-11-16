using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ELearningPlatform.Application.Abstractions.Persistence;

/// <summary>
/// The application's view of the database. EF Core's DbSet already is a repository and
/// SaveChangesAsync already is a unit of work, so services use this directly.
/// </summary>
public interface IAppDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<Teacher> Teachers { get; }
    DbSet<Student> Students { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Course> Courses { get; }
    DbSet<CourseComment> CourseComments { get; }
    DbSet<CourseReaction> CourseReactions { get; }
    DbSet<StudentCourse> StudentCourses { get; }
    DbSet<Video> Videos { get; }
    DbSet<VideoComment> VideoComments { get; }
    DbSet<VideoReaction> VideoReactions { get; }
    DbSet<Exam> Exams { get; }
    DbSet<Question> Questions { get; }
    DbSet<AnswerOption> AnswerOptions { get; }
    DbSet<StudentExam> StudentExams { get; }
    DbSet<StudentAnswer> StudentAnswers { get; }

    DbSet<Wallet> Wallets { get; }
    DbSet<LedgerTransaction> LedgerTransactions { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Payout> Payouts { get; }
    DbSet<CoursePurchase> CoursePurchases { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents { get; }

    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
