namespace ELearningPlatform.Domain.Entities;

public class Course : IAuditable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Hours { get; set; }
    public DateTime CreateAt { get; set; }
    public decimal Price { get; set; }
    public bool IsFree => Price == 0;
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;
    public ICollection<StudentCourse> StudentCourses { get; set; } = [];
    public ICollection<CourseComment> CourseComments { get; set; } = [];
    public ICollection<CourseReaction> CourseReactions { get; set; } = [];
    public ICollection<Video> Videos { get; set; } = [];
    public ICollection<Exam> Exams { get; set; } = [];

}
