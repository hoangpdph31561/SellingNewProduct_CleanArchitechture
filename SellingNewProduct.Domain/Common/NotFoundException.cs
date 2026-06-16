namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Thrown by a domain service when a referenced entity does not exist
/// (e.g. creating a Product for a CategoryId that is not in the database).
/// The API maps this to HTTP 404, separate from <see cref="DomainException"/> (HTTP 400).
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string theMessage) : base(theMessage)
    {
    }
}
