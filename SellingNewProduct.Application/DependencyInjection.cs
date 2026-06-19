using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Application.Common.Behaviors;
using SellingNewProduct.Domain;

namespace SellingNewProduct.Application;

/// <summary>
/// Composition for the Application (CQRS) layer. Registers MediatR (so the API can send
/// commands/queries through <c>ISender</c>), the validation pipeline behavior, and every
/// FluentValidation validator in this assembly. Also pulls in the Domain services that the
/// command handlers delegate their business rules to.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection theServices)
    {
        var aAssembly = typeof(DependencyInjection).Assembly;

        // Business rules live in the Domain layer; handlers are thin adapters over these services.
        theServices.AddDomainServices();

        theServices.AddMediatR(theConfig => theConfig.RegisterServicesFromAssembly(aAssembly));

        // Validation runs as the outermost pipeline step, before any handler.
        theServices.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Command/query validators (request shape). Discovered by ValidationBehavior via DI.
        theServices.AddValidatorsFromAssembly(aAssembly);

        return theServices;
    }
}
