namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// Hashes a plaintext password. The Domain owns this contract so <c>UserWriteService</c>
/// can hash without knowing the algorithm; the concrete implementation is provided
/// by an outer layer (here, the API) and plugged in via DI.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string thePassword);

    /// <summary>
    /// Verifies a plaintext password against a stored hash. Kept on the contract (rather than
    /// re-hashing and comparing in the caller) so the implementation is free to use a scheme whose
    /// hashes are non-deterministic (per-password salt), where a plain string compare would fail.
    /// </summary>
    bool Verify(string thePassword, string thePasswordHash);
}
