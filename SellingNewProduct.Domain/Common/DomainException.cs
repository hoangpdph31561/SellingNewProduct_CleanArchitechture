namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Thrown when a business invariant is violated inside the domain.
/// The API layer translates this into a 400 ProblemDetails response.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string theMessage) : base(theMessage)
    {
    }
}
