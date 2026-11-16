using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("Exams");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DurationInMinutes)
            .IsRequired();

        builder.Property(x => x.StartAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnType("nvarchar")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnType("nvarchar")
            .HasMaxLength(4000)
            .IsRequired(false);

        builder.HasOne(x => x.Course)
            .WithMany(x => x.Exams)
            .HasForeignKey(x => x.CourseId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Teacher)
            .WithMany(x => x.Exams)
            .HasForeignKey(x => x.TeacherId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
