namespace SellingNewProduct.API.Contracts;

public sealed record CreatePaymentRequest(Guid OrderId, decimal Amount, string Currency, int Method);

/// <summary>Starts an online (VNPay) payment for an order; returns the redirect URL to send the customer to.</summary>
public sealed record CreateVnPayPaymentRequest(Guid OrderId, decimal Amount, string OrderInfo);

public sealed record VnPayPaymentUrlResponse(string PaymentUrl);

public sealed record VnPayReturnResponse(
    bool IsValid,
    bool IsSuccessful,
    Guid OrderId,
    decimal Amount,
    string TransactionReference,
    string ResponseCode,
    bool PaymentCompleted);

public sealed record PaymentResponse(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Currency,
    int Method,
    string PaymentStatus,
    DateTime? PaidAtUtc,
    string Status);
