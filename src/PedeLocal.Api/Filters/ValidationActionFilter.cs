using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PedeLocal.Api.Filters;

/// <summary>Runs any registered FluentValidation <see cref="IValidator{T}"/> for each bound action
/// argument before the action executes. FluentValidation's own ASP.NET Core auto-validation package
/// was deprecated by the library's maintainers as of v9/v11 in favor of exactly this pattern — a
/// small global action filter — so this isn't a workaround, it's the currently-recommended wiring.
///
/// Every <c>*RequestValidator</c> class across the Application layer is registered in DI
/// (<c>AddValidatorsFromAssembly</c> in Application/DependencyInjection.cs) but, before this filter
/// existed, nothing ever actually invoked them — see docs/SECURITY_AUDIT.md. A failing validator
/// throws FluentValidation.ValidationException, already mapped by ApiExceptionHandler to
/// 422 VALIDATION_ERROR with a per-field error dictionary.</summary>
public class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }
}
