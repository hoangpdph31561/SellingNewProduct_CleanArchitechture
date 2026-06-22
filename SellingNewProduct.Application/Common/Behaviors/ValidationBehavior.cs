using FluentValidation;
using MediatR;

namespace SellingNewProduct.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline step that validates a request BEFORE its handler runs. It collects every
/// <see cref="IValidator{T}"/> registered for the request type, runs them, and throws a
/// <see cref="ValidationException"/> on the first batch of failures. The API's exception
/// middleware maps that to a 400 <c>ApiResponse</c> — the same contract as before, but now
/// request-shape validation is a cross-cutting concern of the CQRS pipeline (one place),
/// not a controller/filter concern. Requests with no validator simply pass through.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> myValidators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> theValidators)
    {
        myValidators = theValidators;
    }

    public async Task<TResponse> Handle(
        TRequest theRequest,
        RequestHandlerDelegate<TResponse> theNext,
        CancellationToken theCancellationToken)
    {
        if (!myValidators.Any())
        {
            return await theNext();
        }

        var aContext = new ValidationContext<TRequest>(theRequest);

        var aFailures = (await Task.WhenAll(
                myValidators.Select(v => v.ValidateAsync(aContext, theCancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (aFailures.Count != 0)
        {
            throw new ValidationException(aFailures);
        }

        return await theNext();
    }
}
