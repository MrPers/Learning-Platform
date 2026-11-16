using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class StudentExamConfiguration : IEntityTypeConfiguration<StudentExam>
{
    public void Configure(EntityTypeBuilder<StudentExam> builder)
    {
        builder.ToTable("StudentExams");
        builder.HasKey(x => new { x.StudentId, x.ExamId });

        builder.Property(x => x.Degree)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(x => x.SubmittedAt)
           .HasColumnType("datetime2")
           .IsRequired(false);

        builder.HasOne(x => x.Student)
            .WithMany(x => x.StudentExams)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
     
        builder.HasOne(x => x.Exam)
            .WithMany(x => x.StudentExams)
            .HasForeignKey(x => x.ExamId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
