using FluentValidation;

namespace ELearningPlatform.Application.Validators;

/// <summary>Password policy; kept in sync with the Identity password options configured in Infrastructure.</summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Password is required.")
        .MinimumLength(MinLength).WithMessage($"Password must be at least {MinLength} characters.")
        .MaximumLength(MaxLength).WithMessage($"Password must not exceed {MaxLength} characters.")
        .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
        .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
        .Matches("[0-9]").WithMessage("Password must contain a digit.")
        .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a symbol.");
}
