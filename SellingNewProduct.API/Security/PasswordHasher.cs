using System.Security.Cryptography;
using System.Text;
using SellingNewProduct.Domain.Interfaces.Outbound;

namespace SellingNewProduct.API.Security;

/// <summary>
/// Minimal password hashing for the demo. NOT production grade — a real app
/// would use a salted, slow KDF such as PBKDF2 / bcrypt / Argon2.
/// Implements the Domain's <see cref="IPasswordHasher"/> contract.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string thePassword)
    {
        var aBytes = SHA256.HashData(Encoding.UTF8.GetBytes(thePassword));
        return Convert.ToBase64String(aBytes);
    }

    public bool Verify(string thePassword, string thePasswordHash)
    {
        // This scheme is deterministic, so re-hash and compare. Use a fixed-time comparison so the
        // check does not leak how many leading characters matched via its timing.
        var aExpected = Encoding.UTF8.GetBytes(Hash(thePassword));
        var aActual = Encoding.UTF8.GetBytes(thePasswordHash);
        return CryptographicOperations.FixedTimeEquals(aExpected, aActual);
    }
}
