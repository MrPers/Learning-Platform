using System.Collections;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ELearningPlatform.API.Filters;

/// <summary>
/// Runs the registered FluentValidation validator for every action argument (and for each item of a
/// list argument) and answers 400 with validation problem details. Replaces the deprecated
/// FluentValidation.AspNetCore auto-validation; validators run asynchronously.
/// </summary>
public sealed class FluentValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var (name, argument) in context.ActionArguments)
        {
            if (argument == null)
                continue;

            if (GetValidator(argument.GetType()) is { } validator)
            {
                await ValidateAsync(context, validator, argument, prefix: "");
            }
            else if (argument is IEnumerable items and not string)
            {
                var index = 0;
                foreach (var item in items)
                {
                    if (item != null && GetValidator(item.GetType()) is { } itemValidator)
                        await ValidateAsync(context, itemValidator, item, prefix: $"{name}[{index}].");
                    index++;
                }
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = ((ControllerBase)context.Controller).ValidationProblem(context.ModelState);
            return;
        }

        await next();
    }

    private IValidator? GetValidator(Type type)
        => services.GetService(typeof(IValidator<>).MakeGenericType(type)) as IValidator;

    private static async Task ValidateAsync(ActionExecutingContext context, IValidator validator, object instance, string prefix)
    {
        var validationContext = new ValidationContext<object>(instance);
        var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
        foreach (var error in result.Errors)
            context.ModelState.AddModelError(prefix + error.PropertyName, error.ErrorMessage);
    }
}
