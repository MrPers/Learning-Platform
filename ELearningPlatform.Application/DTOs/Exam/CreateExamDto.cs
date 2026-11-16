namespace ELearningPlatform.Application.DTOs.Exam;

public class CreateExamDto
{
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int DurationInMinutes { get; set; }
    public DateTime StartAt { get; set; }
}
