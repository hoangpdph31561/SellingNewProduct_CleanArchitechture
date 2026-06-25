using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Mapping;

/// <summary>
/// Manual mapping for the Order aggregate, including its OrderDetail lines.
/// On update the detail collection is reconciled (add / update / remove).
/// </summary>
internal static class OrderMapper
{
    public static OrderRecord ToRecord(Order theOrder)
    {
        var aRecord = new OrderRecord
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

        foreach (var aDetail in theOrder.Details)
        {
            aRecord.Details.Add(DetailToRecord(theOrder.Id, aDetail));
        }

        return aRecord;
    }

    public static void MapInto(OrderRecord theTarget, Order theSource)
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

        SyncDetails(theTarget, theSource);
    }

    private static void SyncDetails(OrderRecord theTarget, Order theSource)
    {
        var aSourceIds = theSource.Details.Select(d => d.Id).ToHashSet();

        // Remove detail records that no longer exist in the aggregate.
        theTarget.Details.RemoveAll(r => !aSourceIds.Contains(r.Id));

        foreach (var aDetail in theSource.Details)
        {
            var aExisting = theTarget.Details.SingleOrDefault(r => r.Id == aDetail.Id);

            if (aExisting is null)
            {
                theTarget.Details.Add(DetailToRecord(theSource.Id, aDetail));
            }
            else
            {
                aExisting.ProductName = aDetail.ProductName;
                aExisting.UnitPriceAmount = aDetail.UnitPrice.Amount;
                aExisting.UnitPriceCurrency = aDetail.UnitPrice.Currency;
                aExisting.Quantity = aDetail.Quantity;
                aExisting.Status = (int)aDetail.Status;
                aExisting.UpdatedAtUtc = aDetail.UpdatedAtUtc;
            }
        }
    }

    private static OrderDetailRecord DetailToRecord(Guid theOrderId, OrderDetail theDetail) => new()
    {
        Id = theDetail.Id,
        OrderId = theOrderId,
        ProductId = theDetail.ProductId,
        ProductName = theDetail.ProductName,
        UnitPriceAmount = theDetail.UnitPrice.Amount,
        UnitPriceCurrency = theDetail.UnitPrice.Currency,
        Quantity = theDetail.Quantity,
        Status = (int)theDetail.Status,
        CreatedAtUtc = theDetail.CreatedAtUtc,
        UpdatedAtUtc = theDetail.UpdatedAtUtc
    };

    public static Order ToDomain(OrderRecord theRecord)
    {
        var aDetails = theRecord.Details
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
            theRecord.Id,
            theRecord.CustomerId,
            theRecord.EmployeeId,
            (OrderStatus)theRecord.OrderStatus,
            theRecord.OrderDate,
            Address.Create(
                theRecord.ShippingStreet,
                theRecord.ShippingWard,
                theRecord.ShippingDistrict,
                theRecord.ShippingCity,
                theRecord.ShippingCountry),
            Money.Create(theRecord.TotalAmount, theRecord.TotalCurrency),
            aDetails,
            (EntityStatus)theRecord.Status,
            theRecord.CreatedAtUtc,
            theRecord.UpdatedAtUtc);
    }
}
