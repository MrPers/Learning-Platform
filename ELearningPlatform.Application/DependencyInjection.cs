using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ELearningPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.TryAddSingleton(TimeProvider.System);

        // Validators are resolved and run by the API's validation filter (FluentValidation auto-validation is deprecated)
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<AuthTokenIssuer>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IQuestionService, QuestionService>();

        // Payments: ledger + money operations share the request's DbContext (one transaction per operation)
        services.AddScoped<Ledger>();
        services.AddScoped<FinanceTransaction>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IPayoutService, PayoutService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
