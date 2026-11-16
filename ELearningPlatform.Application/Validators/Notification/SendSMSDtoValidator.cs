using FluentValidation;

namespace ELearningPlatform.Application.Validators.Notification;

public class SendSMSDtoValidator : AbstractValidator<SendSMSDto>
{
    public SendSMSDtoValidator()
    {
        RuleFor(x => x.ToPhoneNumber)
             .NotEmpty().WithMessage("Phone Number is required")
             .Matches(@"^(?:\+20|0)(10|11|12|15)[0-9]{8}$").WithMessage("invalid Phone Number format")
             .MaximumLength(200).WithMessage("email must be less than 200 latter");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required")
             .MaximumLength(200).WithMessage("email must be less than 200 latter");
    }
}
