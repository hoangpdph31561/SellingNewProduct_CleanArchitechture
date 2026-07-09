using System.Net;
using FluentValidation;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.API.Middleware;

/// <summary>
/// Translates exceptions into the standard <see cref="ApiResponse"/> error envelope and the
/// matching HTTP status code. This is the ONLY place that knows the HTTP status for each
/// (HTTP-free) domain exception type:
/// - FluentValidation (request-shape errors) and <see cref="DomainException"/> (business
///   rule violations) → 400.
/// - <see cref="ConflictException"/> (uniqueness / state conflict) → 409.
/// - <see cref="NotFoundException"/> (referenced entity missing) → 404.
/// - <see cref="UnauthorizedException"/> (bad / missing credentials) → 401.
/// - anything else → 500.
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
            // One entry per failed rule so the client can show them all.
            await WriteAsync(theContext, HttpStatusCode.BadRequest,
                aValidationException.Errors.Select(e => e.ErrorMessage));
        }
        catch (NotFoundException aNotFoundException)
        {
            await WriteAsync(theContext, HttpStatusCode.NotFound, new[] { aNotFoundException.Message });
        }
        catch (ConflictException aConflictException)
        {
            await WriteAsync(theContext, HttpStatusCode.Conflict, new[] { aConflictException.Message });
        }
        catch (UnauthorizedException aUnauthorizedException)
        {
            await WriteAsync(theContext, HttpStatusCode.Unauthorized, new[] { aUnauthorizedException.Message });
        }
        catch (DomainException aDomainException)
        {
            await WriteAsync(theContext, HttpStatusCode.BadRequest, new[] { aDomainException.Message });
        }
        catch (Exception aException)
        {
            myLogger.LogError(aException, "Unhandled exception");
            await WriteAsync(theContext, HttpStatusCode.InternalServerError,
                new[] { "An unexpected error occurred." });
        }
    }

    private static async Task WriteAsync(HttpContext theContext, HttpStatusCode theStatusCode, IEnumerable<string> theErrors)
    {
        var aResponse = new ApiResponse
        {
            StatusCode = theStatusCode,
            IsSuccess = false,
            ErrorMessages = theErrors.ToList(),
            Result = null
        };

        theContext.Response.StatusCode = (int)theStatusCode;
        theContext.Response.ContentType = "application/json";
        await theContext.Response.WriteAsJsonAsync(aResponse);
    }
}
