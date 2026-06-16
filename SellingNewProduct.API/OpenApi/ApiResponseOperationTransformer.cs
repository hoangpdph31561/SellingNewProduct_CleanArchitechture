using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SellingNewProduct.API.OpenApi;

/// <summary>
/// Keeps the OpenAPI/Swagger document honest about the runtime <c>ApiResponse</c> envelope.
/// At runtime a result filter wraps each payload into the envelope, but the controllers still
/// declare the inner DTO as their return type — so without this the document would show the
/// raw DTO. This transformer rewrites every 2xx JSON response schema into the envelope shape
/// (statusCode / isSuccess / errorMessages / result), with the original DTO schema nested
/// under <c>result</c>.
/// </summary>
internal sealed class ApiResponseOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation theOperation,
        OpenApiOperationTransformerContext theContext,
        CancellationToken theCancellationToken)
    {
        if (theOperation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (aStatusKey, aResponse) in theOperation.Responses)
        {
            if (!int.TryParse(aStatusKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var aCode)
                || aCode is < 200 or > 299
                || aResponse.Content is null)
            {
                continue;
            }

            foreach (var aMedia in aResponse.Content.Values)
            {
                aMedia.Schema = WrapSchema(aMedia.Schema);
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema WrapSchema(IOpenApiSchema? theInner) =>
        new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["statusCode"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["isSuccess"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                ["errorMessages"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                ["result"] = theInner ?? new OpenApiSchema { Type = JsonSchemaType.Object }
            }
        };
}
