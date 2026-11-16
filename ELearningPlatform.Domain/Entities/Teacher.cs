namespace ELearningPlatform.Domain.Entities;

public class Teacher
{
    public int Id { get; set; }
    public Specialization Specialization { get; set; }
    public string ApplicationUserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<Course> Courses { get; set; } = [];
    public ICollection<Exam> Exams { get; set; } = [];

}
