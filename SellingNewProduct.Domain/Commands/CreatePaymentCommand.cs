namespace SellingNewProduct.Domain.Commands;

/// <summary>Input for <c>IPaymentService.CreateAsync</c>. <c>Method</c> is the PaymentMethod enum value.</summary>
public sealed record CreatePaymentCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    int Method);
