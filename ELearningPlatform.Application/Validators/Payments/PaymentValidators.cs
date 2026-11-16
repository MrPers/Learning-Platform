using FluentValidation;
using Microsoft.Extensions.Options;

namespace ELearningPlatform.Application.Validators.Payments;

internal static class MoneyRules
{
    public static IRuleBuilderOptions<T, decimal> ValidMoney<T>(this IRuleBuilder<T, decimal> rule)
        => rule.Must(Money.HasValidScale).WithMessage("Amount can have at most 2 decimal places");

    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule)
        => rule.GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule)
        => rule.InclusiveBetween(1, PaginationExtensions.MaxPageSize).WithMessage($"Page size must be between 1 and {PaginationExtensions.MaxPageSize}");
}

public class CreateTopUpDtoValidator : AbstractValidator<CreateTopUpDto>
{
    public CreateTopUpDtoValidator(IOptions<PaymentsSettings> options)
    {
        var settings = options.Value;
        RuleFor(x => x.Amount)
            .InclusiveBetween(settings.MinTopUp, settings.MaxTopUp)
            .WithMessage($"Amount must be between {settings.MinTopUp} and {settings.MaxTopUp}")
            .ValidMoney();
    }
}

public class RequestPayoutDtoValidator : AbstractValidator<RequestPayoutDto>
{
    public RequestPayoutDtoValidator(IOptions<PaymentsSettings> options)
    {
        var settings = options.Value;
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(settings.MinPayout).WithMessage($"The minimum payout is {settings.MinPayout}")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount is too large")
            .ValidMoney();
    }
}

public class RejectPayoutDtoValidator : AbstractValidator<RejectPayoutDto>
{
    public RejectPayoutDtoValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required")
            .MaximumLength(500).WithMessage("Reason must be at most 500 characters");
    }
}

public class StatementQueryValidator : AbstractValidator<StatementQuery>
{
    public StatementQueryValidator()
    {
        RuleFor(x => x.PageNumber).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From != null && x.To != null).WithMessage("'To' must be after 'From'");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Direction).IsInEnum();
    }
}

public class PaymentQueryValidator : AbstractValidator<PaymentQuery>
{
    public PaymentQueryValidator()
    {
        RuleFor(x => x.PageNumber).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class PurchaseQueryValidator : AbstractValidator<PurchaseQuery>
{
    public PurchaseQueryValidator()
    {
        RuleFor(x => x.PageNumber).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class PayoutQueryValidator : AbstractValidator<PayoutQuery>
{
    public PayoutQueryValidator()
    {
        RuleFor(x => x.PageNumber).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class AuditLogQueryValidator : AbstractValidator<AuditLogQuery>
{
    public AuditLogQueryValidator()
    {
        RuleFor(x => x.PageNumber).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From != null && x.To != null).WithMessage("'To' must be after 'From'");
    }
}
