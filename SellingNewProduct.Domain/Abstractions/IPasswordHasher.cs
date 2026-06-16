namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Hashes a plaintext password. The Domain owns this contract so <c>UserService</c>
/// can hash without knowing the algorithm; the concrete implementation is provided
/// by an outer layer (here, the API) and plugged in via DI.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string thePassword);
}
