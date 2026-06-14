namespace SellingNewProduct.Infrastructure.SqlServer.Models;

/// <summary>
/// SQL Server persistence model for User. Plain POCO — schema is configured
/// with Fluent API in <c>Configurations/UserConfiguration</c>, never annotations.
/// </summary>
internal sealed class UserRecord : BaseRecord
{
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int Role { get; set; }
}
