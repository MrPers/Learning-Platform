namespace ELearningPlatform.Domain.Entities;

public class StudentExam
{
    public double Degree { get; set; }
    public double TotalDegree { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = [];
}
