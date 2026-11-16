using FluentValidation;

namespace ELearningPlatform.Application.Validators.Exam;

public class UpdateExamTimeDtoValidator : AbstractValidator<UpdateExamTimeDto>
{
    public UpdateExamTimeDtoValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.StartAt)
         .Must(startAt => startAt!.Value.ToUniversalTime() >= timeProvider.GetUtcNow().UtcDateTime)
         .WithMessage("Date of exam must be in greater than now")
         .When(x => x.StartAt.HasValue);


        RuleFor(x => x.DurationInMinutes)
            .GreaterThanOrEqualTo(1).WithMessage("Duration In Minutes Must be Greater Than zero ")
            .LessThanOrEqualTo(10080).WithMessage("Duration In Minutes Must be less than 10080 min")// 7 days
            .When(x => x.DurationInMinutes.HasValue);

    }
}
