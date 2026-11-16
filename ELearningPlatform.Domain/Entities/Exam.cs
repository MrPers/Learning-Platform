namespace ELearningPlatform.Domain.Entities;

public class Exam
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int DurationInMinutes { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt => StartAt.AddMinutes(DurationInMinutes);
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public ICollection<Question> Questions { get; set; } = [];
    public ICollection<StudentExam> StudentExams { get; set; } = [];

}
