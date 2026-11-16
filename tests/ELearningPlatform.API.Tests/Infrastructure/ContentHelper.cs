using System.Net;
using ELearningPlatform.Application.DTOs.Course;
using ELearningPlatform.Application.DTOs.Exam;
using ELearningPlatform.Application.DTOs.Media;

namespace ELearningPlatform.API.Tests.Infrastructure;

/// <summary>Creates courses, exams and videos through the API.</summary>
public static class ContentHelper
{
    public static async Task<CourseDto> CreateCourseAsync(this TestUser teacher, string? name = null, decimal price = 10m, int hours = 5)
    {
        var response = await teacher.Client.PostJsonAsync("api/Courses",
            new { name = name ?? TestData.Unique("Course"), description = "About things", hours, price });
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        return await response.ReadAsync<CourseDto>();
    }

    public static async Task<ExamDto> CreateExamAsync(this TestUser teacher, int courseId, DateTime startAt, int durationInMinutes = 60)
    {
        var response = await teacher.Client.PostJsonAsync($"api/courses/{courseId}/Exams",
            new { title = "Midterm", notes = "Good luck", durationInMinutes, startAt });
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        return await response.ReadAsync<ExamDto>();
    }

    public static async Task<VideoDto> UploadVideoAsync(this TestUser teacher, int courseId, string title = "Lesson", byte[]? bytes = null)
    {
        var response = await teacher.Client.PostAsync($"api/Courses/{courseId}/Videos", TestData.VideoForm(bytes ?? TestData.Mp4, title: title));
        await ResponseAssert.StatusAsync(HttpStatusCode.Created, response);
        return await response.ReadAsync<VideoDto>();
    }
}
