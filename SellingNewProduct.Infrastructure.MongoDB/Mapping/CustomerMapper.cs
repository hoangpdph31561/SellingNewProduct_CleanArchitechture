using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.MongoDB.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Mapping;

internal static class CustomerMapper
{
    public static CustomerDocument ToDocument(Customer theCustomer) => new()
    {
        Id = theCustomer.Id,
        FullName = theCustomer.FullName,
        Email = theCustomer.Email.Value,
        PhoneNumber = theCustomer.PhoneNumber,
        Street = theCustomer.DefaultAddress.Street,
        Ward = theCustomer.DefaultAddress.Ward,
        District = theCustomer.DefaultAddress.District,
        City = theCustomer.DefaultAddress.City,
        Country = theCustomer.DefaultAddress.Country,
        UserId = theCustomer.UserId,
        Status = (int)theCustomer.Status,
        CreatedAtUtc = theCustomer.CreatedAtUtc,
        UpdatedAtUtc = theCustomer.UpdatedAtUtc
    };

    public static void MapInto(CustomerDocument theTarget, Customer theSource)
    {
        theTarget.FullName = theSource.FullName;
        theTarget.Email = theSource.Email.Value;
        theTarget.PhoneNumber = theSource.PhoneNumber;
        theTarget.Street = theSource.DefaultAddress.Street;
        theTarget.Ward = theSource.DefaultAddress.Ward;
        theTarget.District = theSource.DefaultAddress.District;
        theTarget.City = theSource.DefaultAddress.City;
        theTarget.Country = theSource.DefaultAddress.Country;
        theTarget.UserId = theSource.UserId;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static Customer ToDomain(CustomerDocument theDocument) => Customer.Rehydrate(
        theDocument.Id,
        theDocument.FullName,
        Email.Create(theDocument.Email),
        theDocument.PhoneNumber,
        Address.Create(theDocument.Street, theDocument.Ward, theDocument.District, theDocument.City, theDocument.Country),
        theDocument.UserId,
        (EntityStatus)theDocument.Status,
        theDocument.CreatedAtUtc,
        theDocument.UpdatedAtUtc);
}
