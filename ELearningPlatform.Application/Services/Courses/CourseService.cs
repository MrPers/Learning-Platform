using Microsoft.EntityFrameworkCore;

namespace ELearningPlatform.Application.Services.Courses;

public class CourseService(IAppDbContext db, TimeProvider timeProvider) : ICourseService
{
    public async Task<Result<CourseDto>> AddAsync(string userId, CreateCourseDto dto, CancellationToken cancellationToken = default)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(x => x.ApplicationUserId == userId, cancellationToken);
        if (teacher == null)
            return Result<CourseDto>.Forbidden("Only teachers can create courses");

        var course = dto.ToEntity();
        course.TeacherId = teacher.Id;
        course.CreateAt = timeProvider.GetUtcNow().UtcDateTime;
        db.Courses.Add(course);
        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(course.Id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var course = await db.Courses.FindAsync([courseId], cancellationToken);
        if (course == null)
            return CourseNotFound();

        // Sales are financial records: a sold course can't be deleted (its purchases reference it)
        if (await db.CoursePurchases.AnyAsync(p => p.CourseId == courseId, cancellationToken))
            return Result.Conflict("The course has purchases and cannot be deleted", ErrorCodes.CourseHasPurchases);

        db.Courses.Remove(course);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Deleted Successfully");
    }

    public async Task<Result<List<CourseDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var coursesDto = await db.Courses.OrderBy(x => x.Id).Select(Mappings.CourseToDto).ToListAsync(cancellationToken);
        return Result<List<CourseDto>>.Success(coursesDto);
    }

    public async Task<Result<PaginatedResult<CourseDto>>> GetAllWithPaginationsAsync(int pageNumber, int pageSize, string baseUrl, CancellationToken cancellationToken = default)
    {
        var page = await db.Courses.OrderBy(x => x.Id)
            .Select(Mappings.CourseToDto)
            .ToPaginatedResultAsync(pageNumber, pageSize, baseUrl, cancellationToken);
        return Result<PaginatedResult<CourseDto>>.Success(page);
    }

    public async Task<Result<CourseDto>> GetByIdAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var courseDto = await db.Courses.Where(x => x.Id == courseId)
            .Select(Mappings.CourseToDto).FirstOrDefaultAsync(cancellationToken);
        if (courseDto == null)
            return Result<CourseDto>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        return Result<CourseDto>.Success(courseDto);
    }

    public async Task<Result> RateCourseAsync(string userId, int courseId, CourseReactionDto dto, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return CourseNotFound();

        if (await db.CourseReactions.AnyAsync(x => x.CourseId == courseId && x.UserId == userId, cancellationToken))
            return Result.Conflict("You have already rated this course", ErrorCodes.AlreadyRated);

        db.CourseReactions.Add(new CourseReaction
        {
            CourseId = courseId,
            UserId = userId,
            IsLike = dto.IsLike,
            Rating = dto.Rating,
            ReactedAt = timeProvider.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Course Rated Successfully");
    }

    public async Task<Result> AddCommentToCourseAsync(string userId, int courseId, AddCommentDto dto, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return CourseNotFound();

        db.CourseComments.Add(new CourseComment
        {
            CourseId = courseId,
            UserId = userId,
            Comment = dto.Comment,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Comment Added Successfully");
    }

    public async Task<Result<List<CourseDto>>> SearchByNameAsync(string courseName, CancellationToken cancellationToken = default)
    {
        var candidates = await db.Courses.Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken);
        var rankedIds = FuzzySearch.RankIds(candidates.Select(c => (c.Id, new[] { c.Name })), courseName);
        if (rankedIds.Count == 0)
            return Result<List<CourseDto>>.Success([]);

        var courses = await db.Courses.Where(x => rankedIds.Contains(x.Id))
            .Select(Mappings.CourseToDto).ToListAsync(cancellationToken);
        return Result<List<CourseDto>>.Success(FuzzySearch.OrderByRank(courses, c => c.Id, rankedIds));
    }

    public async Task<Result<List<CourseDto>>> SearchBySpecializationAsync(Specialization specialization, CancellationToken cancellationToken = default)
    {
        var coursesDto = await db.Courses.Where(c => c.Teacher.Specialization == specialization)
            .OrderBy(x => x.Id)
            .Select(Mappings.CourseToDto).ToListAsync(cancellationToken);
        return Result<List<CourseDto>>.Success(coursesDto);
    }

    public async Task<Result> UpdateAsync(int courseId, UpdateCourseDto dto, CancellationToken cancellationToken = default)
    {
        var course = await db.Courses.FindAsync([courseId], cancellationToken);
        if (course == null)
            return CourseNotFound();

        if (dto.Hours.HasValue)
            course.Hours = dto.Hours.Value;

        if (!string.IsNullOrWhiteSpace(dto.Name))
            course.Name = dto.Name;

        if (!string.IsNullOrWhiteSpace(dto.Description))
            course.Description = dto.Description;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Update Course Successfully");
    }

    public async Task<Result> UpdatePriceAsync(int courseId, UpdateCoursePriceDto dto, CancellationToken cancellationToken = default)
    {
        var course = await db.Courses.FindAsync([courseId], cancellationToken);
        if (course == null)
            return CourseNotFound();

        course.Price = dto.NewPrice;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success("Update Price Successfully");
    }

    public async Task<Result<CourseDto>> GetTopRatedCourseAsync(CancellationToken cancellationToken = default)
    {
        var topRatedId = await db.CourseReactions
            .Where(r => r.Rating != null)
            .GroupBy(r => r.CourseId)
            .Select(g => new { CourseId = g.Key, AvgRating = g.Average(r => (double?)r.Rating) })
            .OrderByDescending(x => x.AvgRating)
            .ThenBy(x => x.CourseId)
            .Select(x => (int?)x.CourseId)
            .FirstOrDefaultAsync(cancellationToken);

        if (topRatedId == null)
            return Result<CourseDto>.NotFound("No rated courses yet", ErrorCodes.CourseNotFound);

        return await GetByIdAsync(topRatedId.Value, cancellationToken);
    }

    public async Task<Result<List<CommentDto>>> GetAllCommentsAsync(int courseId, CancellationToken cancellationToken = default)
    {
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, cancellationToken))
            return Result<List<CommentDto>>.NotFound("Course Not Found", ErrorCodes.CourseNotFound);

        var commentsDto = await db.CourseComments.Where(x => x.CourseId == courseId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(Mappings.CourseCommentToDto).ToListAsync(cancellationToken);
        return Result<List<CommentDto>>.Success(commentsDto);
    }

    private static Result CourseNotFound() => Result.NotFound("Course Not Found", ErrorCodes.CourseNotFound);
}
