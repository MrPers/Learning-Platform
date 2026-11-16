using FluentValidation;

namespace ELearningPlatform.Application.Validators.Course;

public class UpdateCoursePriceDtoValidator : AbstractValidator<UpdateCoursePriceDto>
{
    public UpdateCoursePriceDtoValidator()
    {
        RuleFor(x => x.NewPrice)
          .NotNull().WithMessage("Price is Required")
          .GreaterThanOrEqualTo(0).WithMessage("Price can't be negative")
          .LessThanOrEqualTo(1000000).WithMessage("Price Must be less than 1000000")
          .Must(Money.HasValidScale).WithMessage("Price can have at most 2 decimal places");
    }
}
