using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Mapping;

internal static class CustomerMapper
{
    public static CustomerRecord ToRecord(Customer theCustomer) => new()
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

    public static void MapInto(CustomerRecord theTarget, Customer theSource)
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

    public static Customer ToDomain(CustomerRecord theRecord) => Customer.Rehydrate(
        theRecord.Id,
        theRecord.FullName,
        Email.Create(theRecord.Email),
        theRecord.PhoneNumber,
        Address.Create(theRecord.Street, theRecord.Ward, theRecord.District, theRecord.City, theRecord.Country),
        theRecord.UserId,
        (EntityStatus)theRecord.Status,
        theRecord.CreatedAtUtc,
        theRecord.UpdatedAtUtc);
}
