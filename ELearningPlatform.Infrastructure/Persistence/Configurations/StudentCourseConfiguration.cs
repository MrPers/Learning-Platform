using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearningPlatform.Infrastructure.Persistence.Configurations;

public class StudentCourseConfiguration : IEntityTypeConfiguration<StudentCourse>
{
    public void Configure(EntityTypeBuilder<StudentCourse> builder)
    {
        builder.ToTable("StudentCourses");
        builder.HasKey(x => new { x.StudentId, x.CourseId });

        builder.Property(x => x.EnrolledDate)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasOne(x => x.Course)
            .WithMany(x => x.StudentCourses)
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(x => x.Student)
            .WithMany(x => x.StudentCourses)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
