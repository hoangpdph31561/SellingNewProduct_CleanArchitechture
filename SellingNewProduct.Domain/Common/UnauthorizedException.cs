namespace SellingNewProduct.Domain.Common;

/// <summary>
/// Thrown when a caller's credentials are missing or invalid (e.g. a failed login). HTTP-free like
/// the other domain exceptions; the API maps it to HTTP 401 Unauthorized.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string theMessage) : base(theMessage)
    {
    }
}
