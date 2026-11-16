using FluentValidation;

namespace ELearningPlatform.Application.Validators.Video;

public class UpdateVideoDtoValidator : AbstractValidator<UpdateVideoDto>
{
    public UpdateVideoDtoValidator()
    {
        RuleFor(x => x.Title)
             .MinimumLength(1).WithMessage("Video Title must be at least 2 char.")
             .MaximumLength(500).WithMessage("Video Title must not exceed 500 characters.")
             .When(x => !string.IsNullOrWhiteSpace(x.Title));

        RuleFor(x => x.OriginalName)
             .MinimumLength(1).WithMessage("Original Name must be at least 2 char.")
             .MaximumLength(200).WithMessage("Original Name Title must not exceed 200 characters.")
             .When(x => !string.IsNullOrWhiteSpace(x.OriginalName));

        RuleFor(x => x.Description)
            .MinimumLength(1).WithMessage("Video Description must be at least 2 char.")
            .MaximumLength(4000).WithMessage("Video Description must not exceed 4000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
