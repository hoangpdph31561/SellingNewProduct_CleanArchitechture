namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>
/// SQL Server persistence model for User. Plain POCO — schema is configured
/// with Fluent API in <c>Configurations/UserConfiguration</c>, never annotations.
/// </summary>
internal sealed class UserRecord
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
