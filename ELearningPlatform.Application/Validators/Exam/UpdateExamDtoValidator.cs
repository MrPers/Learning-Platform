using FluentValidation;

namespace ELearningPlatform.Application.Validators.Exam;

public class UpdateExamDtoValidator : AbstractValidator<UpdateExamDto>
{
    public UpdateExamDtoValidator()
    {
        RuleFor(x => x.Title)
            .MinimumLength(1).WithMessage("Exam Title must be at least 2 char.")
            .MaximumLength(1000).WithMessage("Exam Title must not exceed 1000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Title));

        RuleFor(x => x.Notes)
            .MinimumLength(1).WithMessage("Notes must be at least 2 char.")
            .MaximumLength(4000).WithMessage("Notes must not exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));

    }
}
