namespace ELearningPlatform.Application.DTOs.Exam;

public class ResultExamDto
{
    public double Degree { get; set; }
    public double TotalDegree { get; set; }
    public double Percentage { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int NumberOfTFQuestions { get; set; }
    public int NumberOfMCQQuestions { get; set; }
}
