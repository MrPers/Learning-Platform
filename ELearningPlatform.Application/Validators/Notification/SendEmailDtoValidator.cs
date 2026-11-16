using FluentValidation;

namespace ELearningPlatform.Application.Validators.Notification;

public class SendEmailDtoValidator : AbstractValidator<SendEmailDto>
{
    // Attachments are copied into the background job payload, so keep them small
    public const int MaxAttachments = 5;
    public const long MaxAttachmentBytes = 5 * 1024 * 1024;
    public const long MaxTotalAttachmentBytes = 10 * 1024 * 1024;

    public SendEmailDtoValidator()
    {
        RuleFor(x => x.ToEmail)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("invalid Email format")
            .MaximumLength(200).WithMessage("email must be less than 200 latter");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required")
            .MaximumLength(200).WithMessage("Subject must be less than 200 latter");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required");

        RuleFor(x => x.Attachments!)
            .Must(files => files.Count <= MaxAttachments).WithMessage($"At most {MaxAttachments} attachments are allowed.")
            .Must(files => files.Sum(f => f.Length) <= MaxTotalAttachmentBytes).WithMessage("Attachments must not exceed 10 MB in total.")
            .When(x => x.Attachments is { Count: > 0 });

        RuleForEach(x => x.Attachments)
            .Must(file => file.Length > 0).WithMessage("Attachment can't be empty.")
            .Must(file => file.Length <= MaxAttachmentBytes).WithMessage("Each attachment must not exceed 5 MB.")
            .When(x => x.Attachments is { Count: > 0 });
    }
}
