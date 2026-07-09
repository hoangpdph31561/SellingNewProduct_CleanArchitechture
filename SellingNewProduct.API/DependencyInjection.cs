using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SellingNewProduct.API.Filters;
using SellingNewProduct.API.OpenApi;
using SellingNewProduct.API.Security;
using SellingNewProduct.Domain.Interfaces.Outbound;

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

    /// <summary>
    /// Wires up JWT bearer authentication + role-based authorization. The API owns the token format,
    /// so it supplies the Domain <see cref="IAccessTokenGenerator"/> implementation here too. The
    /// signing key, issuer and audience come from the "Jwt" configuration section.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        theServices.Configure<JwtOptions>(theConfiguration.GetSection(JwtOptions.SectionName));
        var aJwt = theConfiguration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

        // Token minting (Domain outbound port) — implemented in the API with the same key.
        theServices.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();

        theServices
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(theOptions =>
            {
                theOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = aJwt.Issuer,
                    ValidAudience = aJwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(aJwt.SigningKey)),
                    // No grace window — an expired token is rejected the instant it expires.
                    ClockSkew = TimeSpan.Zero
                };
            });

        theServices.AddAuthorization();

        return theServices;
    }
}
