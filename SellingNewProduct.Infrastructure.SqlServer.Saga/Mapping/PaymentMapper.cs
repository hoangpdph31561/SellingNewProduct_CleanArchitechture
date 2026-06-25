using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.ValueObjects;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Mapping;

internal static class PaymentMapper
{
    public static PaymentRecord ToRecord(Payment thePayment) => new()
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

    public static void MapInto(PaymentRecord theTarget, Payment theSource)
    {
        theTarget.Amount = theSource.Amount.Amount;
        theTarget.Currency = theSource.Amount.Currency;
        theTarget.Method = (int)theSource.Method;
        theTarget.PaymentStatus = (int)theSource.PaymentStatus;
        theTarget.PaidAtUtc = theSource.PaidAtUtc;
        theTarget.Status = (int)theSource.Status;
        theTarget.UpdatedAtUtc = theSource.UpdatedAtUtc;
    }

    public static Payment ToDomain(PaymentRecord theRecord) => Payment.Rehydrate(
        theRecord.Id,
        theRecord.OrderId,
        Money.Create(theRecord.Amount, theRecord.Currency),
        (PaymentMethod)theRecord.Method,
        (PaymentStatus)theRecord.PaymentStatus,
        theRecord.PaidAtUtc,
        (EntityStatus)theRecord.Status,
        theRecord.CreatedAtUtc,
        theRecord.UpdatedAtUtc);
}
