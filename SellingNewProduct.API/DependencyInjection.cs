using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.API.Filters;
using SellingNewProduct.API.OpenApi;
using SellingNewProduct.API.Security;
using SellingNewProduct.API.Validators;
using SellingNewProduct.Domain.Abstractions;

namespace SellingNewProduct.API;

/// <summary>
/// Composition for the API (presentation) layer — the web-facing concerns only:
/// MVC controllers + the ApiResponse wrapping filter, the OpenAPI document + its envelope
/// transformer, the FluentValidation request validators, and the API's implementation of the
/// Domain <see cref="IPasswordHasher"/> contract. Mirrors <c>AddDomainServices()</c> and
/// <c>AddSqlServerInfrastructure()</c> so each layer owns its own registrations; the DB choice
/// stays in the composition root (Program.cs).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection theServices)
    {
        // The result filter wraps every successful response into the standard ApiResponse envelope.
        theServices.AddControllers(theOptions => theOptions.Filters.Add<ApiResponseWrapperFilter>());

        // Make the OpenAPI document reflect the ApiResponse envelope the result filter adds at runtime.
        theServices.AddOpenApi(theOptions => theOptions.AddOperationTransformer<ApiResponseOperationTransformer>());

        // API-level validators (request shape).
        theServices.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

        // Password hashing: API provides the implementation of the Domain contract.
        theServices.AddSingleton<IPasswordHasher, PasswordHasher>();

        return theServices;
    }
}
