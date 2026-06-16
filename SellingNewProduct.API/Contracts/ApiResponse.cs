using System.Net;
using System.Text.Json.Serialization;

namespace SellingNewProduct.API.Contracts;

/// <summary>
/// Standard envelope wrapping EVERY API response so clients always get the same shape:
/// HTTP status, a success flag, a list of error messages and the payload. Success responses
/// are produced by the response-wrapping result filter; error responses by the exception
/// middleware. This is an API-layer concern only — the Domain never sees it.
/// </summary>
public class ApiResponse
{
    [JsonPropertyName("statusCode")]
    public HttpStatusCode StatusCode { get; set; }

    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; } = true;

    [JsonPropertyName("errorMessages")]
    public List<string> ErrorMessages { get; set; } = new();

    [JsonPropertyName("result")]
    public object? Result { get; set; }
}

/// <summary>
/// Strongly-typed envelope: same JSON shape as <see cref="ApiResponse"/> but with a typed
/// <c>Result</c> so OpenAPI/Swagger can describe the payload of each endpoint. It is a
/// SEPARATE class (not a subclass) on purpose: shadowing <c>Result</c> via inheritance would
/// make System.Text.Json emit two "result" properties. The runtime filter builds the
/// non-generic form; this type drives the documentation.
/// </summary>
public sealed class ApiResponse<T>
{
    [JsonPropertyName("statusCode")]
    public HttpStatusCode StatusCode { get; set; }

    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; } = true;

    [JsonPropertyName("errorMessages")]
    public List<string> ErrorMessages { get; set; } = new();

    [JsonPropertyName("result")]
    public T? Result { get; set; }
}
