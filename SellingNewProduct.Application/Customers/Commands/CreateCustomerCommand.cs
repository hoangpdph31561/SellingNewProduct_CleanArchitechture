using MediatR;
using SellingNewProduct.Domain.Customers;

namespace SellingNewProduct.Application.Customers;

/// <summary>Write-side command: create a customer. Address is carried as flat fields.</summary>
public sealed record CreateCustomerCommand(
    string FullName,
    string Email,
    string PhoneNumber,
    string Street,
    string Ward,
    string District,
    string City,
    string Country,
    Guid? UserId) : IRequest<Customer>;
