using FluentValidation;

namespace ELearningPlatform.Application.Validators.Course;

public class UpdateCourseDtoValidator : AbstractValidator<UpdateCourseDto>
{
    public UpdateCourseDtoValidator()
    {
        RuleFor(x => x.Name)
            .MinimumLength(2).WithMessage("Course name must be at least 2 char.")
            .MaximumLength(100).WithMessage("Course name must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.Description)
            .MinimumLength(2).WithMessage("Course Description must be at least 2 char.")
            .MaximumLength(4000).WithMessage("Course Description must not exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Hours)
            .GreaterThanOrEqualTo(1).WithMessage("Hours Must be Greater Than zero")
            .LessThanOrEqualTo(10000).WithMessage("Hours Must be less than 10000")
            .When(x => x.Hours.HasValue);

    }
}
