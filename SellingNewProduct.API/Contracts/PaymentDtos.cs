namespace SellingNewProduct.API.Contracts;

public sealed record CreatePaymentRequest(Guid OrderId, decimal Amount, string Currency, int Method);

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Currency,
    int Method,
    string PaymentStatus,
    DateTime? PaidAtUtc,
    string Status);
