using FluentValidation;

namespace ELearningPlatform.Application.Validators.Question;

public class CreateMcqQuestionDtoValidator : AbstractValidator<CreateMcqQuestionDto>
{
    public CreateMcqQuestionDtoValidator()
    {
        Include(new CreateQuestionDtoValidator());


        RuleFor(q => q.Option1)
          .NotEmpty().WithMessage("Option 1 is required.")
          .MaximumLength(1000).WithMessage("Option 1 must not exceed 1000 characters.");

        RuleFor(q => q.Option2)
            .NotEmpty().WithMessage("Option 2 is required.")
            .MaximumLength(1000).WithMessage("Option 2 must not exceed 1000 characters.");
        
        RuleFor(q => q.Option3)
          .NotEmpty().WithMessage("Option 3 is required.")
          .MaximumLength(1000).WithMessage("Option 3 must not exceed 1000 characters.");

        RuleFor(q => q.Option4)
            .NotEmpty().WithMessage("Option 4 is required.")
            .MaximumLength(1000).WithMessage("Option 4 must not exceed 1000 characters.");

        RuleFor(q => q.CorrectAnswerIndex)
            .InclusiveBetween(1, 4).WithMessage("Correct Answer Index must be between 1 and 4.");

    }
}
