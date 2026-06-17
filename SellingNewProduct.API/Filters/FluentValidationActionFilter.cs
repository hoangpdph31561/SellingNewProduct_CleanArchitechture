using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SellingNewProduct.API.Filters;

/// <summary>
/// Runs FluentValidation automatically for every action argument that has a registered
/// <see cref="IValidator{T}"/>, BEFORE the action body executes. Controllers therefore no
/// longer inject validators or call <c>ValidateAndThrowAsync</c> — request-shape validation is
/// a cross-cutting concern handled here in one place.
///
/// How it "knows" what to validate: for each argument it asks the DI container for an
/// <c>IValidator&lt;[argument type]&gt;</c>. Primitive route/query parameters (Guid, int,
/// CancellationToken...) have no validator registered and are simply skipped; request DTOs do,
/// so they get validated. A failure throws <see cref="ValidationException"/>, which the
/// exception middleware maps to a 400 <c>ApiResponse</c> — same path as before.
/// </summary>
internal sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext theContext, ActionExecutionDelegate theNext)
    {
        var aServices = theContext.HttpContext.RequestServices;

        foreach (var aArgument in theContext.ActionArguments.Values)
        {
            if (aArgument is null)
            {
                continue;
            }

            var aValidatorType = typeof(IValidator<>).MakeGenericType(aArgument.GetType());

            if (aServices.GetService(aValidatorType) is IValidator aValidator)
            {
                var aResult = await aValidator.ValidateAsync(new ValidationContext<object>(aArgument));
                if (!aResult.IsValid)
                {
                    throw new ValidationException(aResult.Errors);
                }
            }
        }

        await theNext();
    }
}
