using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.API.Filters;
using SellingNewProduct.API.OpenApi;
using SellingNewProduct.API.Security;
using SellingNewProduct.Domain.Abstractions;

namespace SellingNewProduct.API;

/// <summary>
/// Composition for the API (presentation) layer — the web-facing concerns only:
/// MVC controllers + the ApiResponse wrapping filter, the OpenAPI document + its envelope
/// transformer, and the API's implementation of the Domain <see cref="IPasswordHasher"/>
/// contract. Request validation now lives in the Application CQRS pipeline (ValidationBehavior),
/// not here. The DB choice stays in the composition root (Program.cs).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection theServices)
    {
        // The result filter wraps every successful action payload in the ApiResponse envelope.
        theServices.AddControllers(theOptions =>
        {
            theOptions.Filters.Add<ApiResponseWrapperFilter>();
        });

        // Make the OpenAPI document reflect the ApiResponse envelope the result filter adds at runtime.
        theServices.AddOpenApi(theOptions => theOptions.AddOperationTransformer<ApiResponseOperationTransformer>());

        // Password hashing: API provides the implementation of the Domain contract.
        theServices.AddSingleton<IPasswordHasher, PasswordHasher>();

        return theServices;
    }
}
