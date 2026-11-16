namespace ELearningPlatform.Domain.Entities;

public class Student
{
    public int Id { get; set; }
    public Level Level { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<StudentCourse> StudentCourses { get; set; } = [];
    public ICollection<StudentExam> StudentExams { get; set; } = [];

}
