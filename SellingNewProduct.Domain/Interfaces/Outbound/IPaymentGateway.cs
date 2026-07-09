namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>What the caller must supply to start an online payment.</summary>
public sealed record PaymentGatewayRequest(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string OrderInfo,
    string ClientIpAddress);

/// <summary>The redirect URL the customer's browser must be sent to in order to pay.</summary>
public sealed record PaymentGatewayResult(string PaymentUrl);

/// <summary>
/// The verified outcome of a gateway return/IPN callback. <see cref="IsValid"/> is the signature
/// check (was this really sent by the gateway, untampered); <see cref="IsSuccessful"/> is the payment
/// result once the signature is trusted.
/// </summary>
public sealed record PaymentCallbackResult(
    bool IsValid,
    bool IsSuccessful,
    Guid OrderId,
    decimal Amount,
    string TransactionReference,
    string ResponseCode);

/// <summary>
/// Outbound port for an online payment gateway (redirect model, e.g. VNPay). The Domain owns the
/// contract so the order/payment flow can start a payment and verify its callback without knowing the
/// provider; the concrete adapter (signing, provider URLs) lives in an infrastructure project.
/// Both members are pure/local (build a signed URL, verify a signature) — no outbound network call,
/// so no circuit breaker is needed here.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Builds the signed redirect URL to send the customer to the gateway's payment page.</summary>
    PaymentGatewayResult CreatePayment(PaymentGatewayRequest theRequest);

    /// <summary>Verifies a return/IPN callback's signature and reports the payment outcome.</summary>
    PaymentCallbackResult VerifyCallback(IReadOnlyDictionary<string, string> theCallbackParameters);
}
