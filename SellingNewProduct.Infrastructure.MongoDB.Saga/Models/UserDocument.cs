namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

/// <summary>MongoDB persistence model for User.</summary>
internal sealed class UserDocument : BaseDocument
{
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Email { get; set; } = default!;
    public int Role { get; set; }
}
