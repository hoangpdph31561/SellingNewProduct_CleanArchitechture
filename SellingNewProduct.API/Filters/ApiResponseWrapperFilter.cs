using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SellingNewProduct.API.Contracts;

namespace SellingNewProduct.API.Filters;

/// <summary>
/// Wraps every successful controller result into the standard <see cref="ApiResponse"/>
/// envelope, so controllers can keep returning plain DTOs (<c>Ok(dto)</c>,
/// <c>CreatedAtAction(...)</c>, <c>NotFound()</c>) while clients always receive the same
/// shape. Error responses thrown as exceptions are handled separately by the exception
/// middleware. This is the one place that knows about the envelope on the success path.
/// </summary>
internal sealed class ApiResponseWrapperFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext theContext)
    {
        switch (theContext.Result)
        {
            // Ok(dto), CreatedAtAction(...), etc. — has a payload.
            case ObjectResult aObjectResult:
                if (aObjectResult.Value is ApiResponse)
                {
                    return; // Already wrapped (e.g. wrapped manually) — don't double-wrap.
                }

                var aStatusCode = aObjectResult.StatusCode ?? StatusCodes.Status200OK;
                aObjectResult.Value = Wrap(aStatusCode, aObjectResult.Value);
                break;

            // NoContent(), NotFound(), Ok() with no body — status only, no payload.
            case StatusCodeResult aStatusCodeResult:
                var aBareCode = aStatusCodeResult.StatusCode;
                theContext.Result = new ObjectResult(Wrap(aBareCode, null))
                {
                    // 204 cannot carry a body; once wrapped it becomes a 200 with the envelope.
                    StatusCode = aBareCode == StatusCodes.Status204NoContent ? StatusCodes.Status200OK : aBareCode
                };
                break;
        }
    }

    public void OnResultExecuted(ResultExecutedContext theContext)
    {
    }

    private static ApiResponse Wrap(int theStatusCode, object? theValue)
    {
        var aIsSuccess = theStatusCode is >= 200 and <= 299;

        var aResponse = new ApiResponse
        {
            StatusCode = (HttpStatusCode)theStatusCode,
            IsSuccess = aIsSuccess,
            Result = aIsSuccess ? theValue : null
        };

        if (!aIsSuccess)
        {
            // A bodyless failure (e.g. NotFound()) still gets a human-readable message.
            aResponse.ErrorMessages.Add(theValue?.ToString() ?? ReasonFor(theStatusCode));
        }

        return aResponse;
    }

    private static string ReasonFor(int theStatusCode) => theStatusCode switch
    {
        StatusCodes.Status404NotFound => "Resource not found.",
        StatusCodes.Status401Unauthorized => "Unauthorized.",
        StatusCodes.Status403Forbidden => "Forbidden.",
        _ => "Request failed."
    };
}
