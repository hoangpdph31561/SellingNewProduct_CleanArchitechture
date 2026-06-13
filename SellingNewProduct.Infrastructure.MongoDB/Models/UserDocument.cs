namespace SellingNewProduct.Infrastructure.MongoDB.Models;

/// <summary>
/// MongoDB persistence model for User. Plain POCO — mapping is configured with
/// Fluent API (ToCollection) in <c>Configurations/UserConfiguration</c>.
/// </summary>
internal sealed class UserDocument
{
    public Guid Id { get; set; }
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int Role { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
