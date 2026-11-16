namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class StudentAnswerConfiguration : IEntityTypeConfiguration<StudentAnswer>
{
    public void Configure(EntityTypeBuilder<StudentAnswer> builder)
    {
        builder.ToTable("StudentAnswers");

        builder.HasKey(x => new { x.StudentId, x.ExamId, x.QuestionNumber });

        builder.HasOne(x => x.StudentExam)
            .WithMany(x => x.StudentAnswers)
            .HasForeignKey(x => new { x.StudentId, x.ExamId })
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(x => x.Question)
            .WithMany(x => x.StudentAnswers)
            .HasForeignKey(x => new { x.ExamId, x.QuestionNumber })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(x => x.AnswerOptions)
            .WithMany()
            .HasForeignKey(x => new { x.ExamId, x.QuestionNumber, x.OptionNumber })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
