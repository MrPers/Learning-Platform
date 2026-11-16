using FluentValidation;

namespace ELearningPlatform.Application.Validators.Question;

public class CreateQuestionDtoValidator : AbstractValidator<CreateQuestionDto>
{
    public CreateQuestionDtoValidator()
    {
        RuleFor(q => q.Header)
            .NotEmpty().WithMessage("Question header is required.")
            .MaximumLength(1000).WithMessage("Question header must not exceed 1000 characters.");

        RuleFor(q => q.Mark)
            .GreaterThan(0).WithMessage("Mark must be greater than 0.")
            .LessThanOrEqualTo(10000).WithMessage("Mark must be less than or equal to 10000.");
    }
}
