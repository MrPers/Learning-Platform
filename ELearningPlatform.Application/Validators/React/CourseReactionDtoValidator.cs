using FluentValidation;

namespace ELearningPlatform.Application.Validators.React;

public class CourseReactionDtoValidator:AbstractValidator<CourseReactionDto>
{
    public CourseReactionDtoValidator()
    {
        RuleFor(x => x.Rating)
            .IsInEnum().WithMessage("Invalid rating value.");
    }
}
