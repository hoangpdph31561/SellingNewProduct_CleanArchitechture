using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.API.Middleware;

/// <summary>
/// Translates exceptions into RFC7807 ProblemDetails:
/// - FluentValidation (API-level format errors) and DomainException (business
///   rule violations) both become 400.
/// </summary>
internal sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate myRequestDelegate;
    private readonly ILogger<ExceptionHandlingMiddleware> myLogger;

    public ExceptionHandlingMiddleware(RequestDelegate theNext, ILogger<ExceptionHandlingMiddleware> theLogger)
    {
        myRequestDelegate = theNext;
        myLogger = theLogger;
    }

    public async Task InvokeAsync(HttpContext theContext)
    {
        try
        {
            await myRequestDelegate(theContext);
        }
        catch (ValidationException aValidationException)
        {
            await WriteProblemAsync(theContext, StatusCodes.Status400BadRequest,
                "Validation failed",
                string.Join(" | ", aValidationException.Errors.Select(e => e.ErrorMessage)));
        }
        catch (DomainException aDomainException)
        {
            await WriteProblemAsync(theContext, StatusCodes.Status400BadRequest,
                "Business rule violated", aDomainException.Message);
        }
        catch (Exception aException)
        {
            myLogger.LogError(aException, "Unhandled exception");
            await WriteProblemAsync(theContext, StatusCodes.Status500InternalServerError,
                "Unexpected error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext theContext, int theStatusCode, string theTitle, string theDetail)
    {
        var aProblem = new ProblemDetails
        {
            Status = theStatusCode,
            Title = theTitle,
            Detail = theDetail
        };

        theContext.Response.StatusCode = theStatusCode;
        theContext.Response.ContentType = "application/problem+json";
        await theContext.Response.WriteAsJsonAsync(aProblem);
    }
}
