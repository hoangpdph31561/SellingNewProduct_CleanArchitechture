using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Application.Common.Behaviors;

namespace SellingNewProduct.Application;

/// <summary>
/// Composition for the Application (CQRS) layer. Registers MediatR (so the API can send
/// commands/queries through <c>ISender</c>), the validation pipeline behavior, and every
/// FluentValidation validator in this assembly. Replaces the old <c>AddDomainServices()</c>;
/// the API now depends on handlers, never on hand-written domain services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection theServices)
    {
        var aAssembly = typeof(DependencyInjection).Assembly;

        theServices.AddMediatR(theConfig => theConfig.RegisterServicesFromAssembly(aAssembly));

        // Validation runs as the outermost pipeline step, before any handler.
        theServices.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Command/query validators (request shape). Discovered by ValidationBehavior via DI.
        theServices.AddValidatorsFromAssembly(aAssembly);

        return theServices;
    }
}
