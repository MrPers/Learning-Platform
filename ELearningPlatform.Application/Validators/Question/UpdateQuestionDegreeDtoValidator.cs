using FluentValidation;

namespace ELearningPlatform.Application.Validators.Question;

public class UpdateQuestionDegreeDtoValidator : AbstractValidator<UpdateQuestionDegreeDto>
{
    public UpdateQuestionDegreeDtoValidator()
    {
        RuleFor(q => q.Mark)
                .GreaterThan(0).WithMessage("Mark must be greater than 0.")
                .LessThanOrEqualTo(10000).WithMessage("Mark must be less than or equal to 10000.");
    }
}
