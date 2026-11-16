using FluentValidation;

namespace ELearningPlatform.Application.Validators.Exam;

public class SubmitExamDtoValidator : AbstractValidator<SubmitExamDto>
{
    public SubmitExamDtoValidator()
    {
        RuleFor(x => x.QuestionNumber)
            .InclusiveBetween((byte)1, (byte)100).WithMessage("Question number must be between 1 and 100");

        RuleFor(x => x.OptionNumber)
             .NotEmpty().WithMessage("option number is required")
             .InclusiveBetween((byte)1, (byte)4).WithMessage("Option number must be between 1 and 4");
    }
}
