using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.MongoDB.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Mapping;

internal static class PaymentMapper
{
    public static PaymentDocument ToDocument(Payment thePayment) => new()
    {
        Id = thePayment.Id,
        OrderId = thePayment.OrderId,
        Amount = thePayment.Amount.Amount,
        Currency = thePayment.Amount.Currency,
        Method = (int)thePayment.Method,
        PaymentStatus = (int)thePayment.PaymentStatus,
        PaidAtUtc = thePayment.PaidAtUtc,
        Status = (int)thePayment.Status,
        CreatedAtUtc = thePayment.CreatedAtUtc,
        UpdatedAtUtc = thePayment.UpdatedAtUtc
    };

    public static void MapInto(PaymentDocument theTarget, Payment theSource)
    {
        theTarget.Amount = theSource.Amount.Amount;
        theTarget.Currency = theSource.Amount.Currency;
        theTarget.Method = (int)theSource.Method;
        theTarget.PaymentStatus = (int)theSource.PaymentStatus;
        theTarget.PaidAtUtc = theSource.PaidAtUtc;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static Payment ToDomain(PaymentDocument theDocument) => Payment.Rehydrate(
        theDocument.Id,
        theDocument.OrderId,
        Money.Create(theDocument.Amount, theDocument.Currency),
        (PaymentMethod)theDocument.Method,
        (PaymentStatus)theDocument.PaymentStatus,
        theDocument.PaidAtUtc,
        (EntityStatus)theDocument.Status,
        theDocument.CreatedAtUtc,
        theDocument.UpdatedAtUtc);
}
