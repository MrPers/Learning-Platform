using FluentValidation;

namespace ELearningPlatform.Application.Validators.Question;

public class CreateTFQuestionDtoValidator : AbstractValidator<CreateTFQuestionDto>
{
    public CreateTFQuestionDtoValidator()
    {
        Include(new CreateQuestionDtoValidator());

        RuleFor(x => x.IsTrue)
       .NotNull().WithMessage("You must specify whether the answer is true or false.");
    }
}
