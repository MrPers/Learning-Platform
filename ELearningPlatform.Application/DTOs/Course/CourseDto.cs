namespace ELearningPlatform.Application.DTOs.Course;

public class CourseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Hours { get; set; }
    public DateTime CreateAt { get; set; }
    public decimal Price { get; set; }
    public bool IsFree => Price == 0;
    public string TeacherName { get; set; } = string.Empty;
    public Specialization Specialization { get; set; }
    public int NumberOfVides { get; set; }
    public int NumberOfLike { get; set; }
    public int NumberOfDislike { get; set; }
    public int NumberOfComment { get; set; }
}
