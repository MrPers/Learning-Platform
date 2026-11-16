using FluentValidation;

namespace ELearningPlatform.Application.Validators.Course;

public class CreateCourseDtoValidator : AbstractValidator<CreateCourseDto>
{
    public CreateCourseDtoValidator()
    {
        RuleFor(x => x.Price)
            .NotNull().WithMessage("Price is Required")
            .GreaterThanOrEqualTo(0).WithMessage("Price can't be negative")
            .LessThanOrEqualTo(1000000).WithMessage("Price Must be less than 1000000")
          .Must(Money.HasValidScale).WithMessage("Price can have at most 2 decimal places");

        RuleFor(x => x.Hours)
            .NotEmpty().WithMessage("Hours can't be null")
            .GreaterThanOrEqualTo(1).WithMessage("Hours Must be Greater Than zero")
            .LessThanOrEqualTo(10000).WithMessage("Hours Must be less than 10000");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Original Name is required")
            .MinimumLength(2).WithMessage("Course name must be at least 2 char.")
            .MaximumLength(100).WithMessage("Course name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MinimumLength(2).WithMessage("Course Description must be at least 2 char.")
            .MaximumLength(4000).WithMessage("Course Description must not exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description)); ;
       
    }
}
