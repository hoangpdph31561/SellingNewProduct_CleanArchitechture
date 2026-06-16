namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Thrown when an operation conflicts with the current state — most often a uniqueness
/// violation (e.g. creating a Category whose name already exists). HTTP-free like the other
/// domain exceptions; the API maps it to HTTP 409 Conflict.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string theMessage) : base(theMessage)
    {
    }
}
