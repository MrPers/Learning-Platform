namespace ELearningPlatform.Application.DTOs.Course;

public class CreateCourseDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Hours { get; set; }
    public decimal Price { get; set; }
}
