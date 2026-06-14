namespace SellingNewProduct.Infrastructure.MongoDB.Models;

/// <summary>
/// MongoDB persistence model for User. Plain POCO — mapping is configured with
/// Fluent API (ToCollection) in <c>Configurations/UserConfiguration</c>.
/// </summary>
internal sealed class UserDocument : BaseDocument
{
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int Role { get; set; }
}
