using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.MongoDB.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Mapping;

/// <summary>
/// Manual mapping for the Order aggregate. In MongoDB the details are an
/// embedded array, so on update we simply rebuild the whole nested list.
/// </summary>
internal static class OrderMapper
{
    public static OrderDocument ToDocument(Order theOrder)
    {
        var aDocument = new OrderDocument
        {
            Id = theOrder.Id,
            CustomerId = theOrder.CustomerId,
            EmployeeId = theOrder.EmployeeId,
            OrderStatus = (int)theOrder.OrderStatus,
            OrderDate = theOrder.OrderDate,
            ShippingStreet = theOrder.ShippingAddress.Street,
            ShippingWard = theOrder.ShippingAddress.Ward,
            ShippingDistrict = theOrder.ShippingAddress.District,
            ShippingCity = theOrder.ShippingAddress.City,
            ShippingCountry = theOrder.ShippingAddress.Country,
            TotalAmount = theOrder.TotalAmount.Amount,
            TotalCurrency = theOrder.TotalAmount.Currency,
            Status = (int)theOrder.Status,
            CreatedAtUtc = theOrder.CreatedAtUtc,
            UpdatedAtUtc = theOrder.UpdatedAtUtc
        };

        aDocument.Details = theOrder.Details.Select(ToDetailDocument).ToList();
        return aDocument;
    }

    public static void MapInto(OrderDocument theTarget, Order theSource)
    {
        theTarget.OrderStatus = (int)theSource.OrderStatus;
        theTarget.OrderDate = theSource.OrderDate;
        theTarget.ShippingStreet = theSource.ShippingAddress.Street;
        theTarget.ShippingWard = theSource.ShippingAddress.Ward;
        theTarget.ShippingDistrict = theSource.ShippingAddress.District;
        theTarget.ShippingCity = theSource.ShippingAddress.City;
        theTarget.ShippingCountry = theSource.ShippingAddress.Country;
        theTarget.TotalAmount = theSource.TotalAmount.Amount;
        theTarget.TotalCurrency = theSource.TotalAmount.Currency;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;

        theTarget.Details = theSource.Details.Select(ToDetailDocument).ToList();
    }

    private static OrderDetailDocument ToDetailDocument(OrderDetail theDetail) => new()
    {
        Id = theDetail.Id,
        ProductId = theDetail.ProductId,
        ProductName = theDetail.ProductName,
        UnitPriceAmount = theDetail.UnitPrice.Amount,
        UnitPriceCurrency = theDetail.UnitPrice.Currency,
        Quantity = theDetail.Quantity,
        Status = (int)theDetail.Status,
        CreatedAtUtc = theDetail.CreatedAtUtc,
        UpdatedAtUtc = theDetail.UpdatedAtUtc
    };

    public static Order ToDomain(OrderDocument theDocument)
    {
        var aDetails = theDocument.Details
            .Select(d => OrderDetail.Rehydrate(
                d.Id,
                d.ProductId,
                d.ProductName,
                Money.Create(d.UnitPriceAmount, d.UnitPriceCurrency),
                d.Quantity,
                (EntityStatus)d.Status,
                d.CreatedAtUtc,
                d.UpdatedAtUtc))
            .ToList();

        return Order.Rehydrate(
            theDocument.Id,
            theDocument.CustomerId,
            theDocument.EmployeeId,
            (OrderStatus)theDocument.OrderStatus,
            theDocument.OrderDate,
            Address.Create(
                theDocument.ShippingStreet,
                theDocument.ShippingWard,
                theDocument.ShippingDistrict,
                theDocument.ShippingCity,
                theDocument.ShippingCountry),
            Money.Create(theDocument.TotalAmount, theDocument.TotalCurrency),
            aDetails,
            (EntityStatus)theDocument.Status,
            theDocument.CreatedAtUtc,
            theDocument.UpdatedAtUtc);
    }
}
