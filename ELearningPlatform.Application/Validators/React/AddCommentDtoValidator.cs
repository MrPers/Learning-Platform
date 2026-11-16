using FluentValidation;

namespace ELearningPlatform.Application.Validators.React;

public class AddCommentDtoValidator: AbstractValidator<AddCommentDto>
{
    public AddCommentDtoValidator()
    {
        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("Comment cannot be empty.")
            .MaximumLength(1000).WithMessage("Comment cannot exceed 1000 characters.");
    }
}
