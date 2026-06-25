namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

/// <summary>MongoDB persistence model for Customer.</summary>
internal sealed class CustomerDocument : BaseDocument
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string Street { get; set; } = default!;
    public string Ward { get; set; } = default!;
    public string District { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public Guid? UserId { get; set; }
}
