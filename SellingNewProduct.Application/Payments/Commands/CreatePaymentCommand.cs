using MediatR;
using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Application.Payments;

/// <summary>Write-side command: record a payment against an order. <c>Method</c> is the PaymentMethod enum value.</summary>
public sealed record CreatePaymentCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    int Method) : IRequest<Payment>;
