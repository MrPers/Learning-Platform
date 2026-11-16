using FluentValidation;

namespace ELearningPlatform.Application.Validators.Exam;

public class CreateExamDtoValidator : AbstractValidator<CreateExamDto>
{
    public CreateExamDtoValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.StartAt)
         .Must(startAt => startAt.ToUniversalTime() >= timeProvider.GetUtcNow().UtcDateTime)
         .WithMessage("Date of exam must be in greater than now");

        RuleFor(x => x.DurationInMinutes)
            .NotEmpty().WithMessage("Duration In Minutes can't be null")
            .GreaterThanOrEqualTo(1).WithMessage("Duration In Minutes Must be Greater Than zero ")
            .LessThanOrEqualTo(10080).WithMessage("Duration In Minutes Must be less than 10080 min");// 7 days 

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MinimumLength(1).WithMessage("Exam Title must be at least 2 char.")
            .MaximumLength(1000).WithMessage("Exam Title must not exceed 1000 characters.");

        RuleFor(x => x.Notes)
            .MinimumLength(1).WithMessage("Notes must be at least 2 char.")
            .MaximumLength(4000).WithMessage("Notes must not exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes)); 
    }
}
