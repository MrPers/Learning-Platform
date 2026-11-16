namespace ELearningPlatform.Application.DTOs.Exam;

public class ExamDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int DurationInMinutes { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int NumberOfQuestions { get; set; }
    public double TotalMark { get; set; }
    public double PassMark { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
}
