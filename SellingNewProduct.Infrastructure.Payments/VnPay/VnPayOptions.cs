namespace SellingNewProduct.Infrastructure.Payments.VnPay;

/// <summary>
/// Binds the <c>VnPay</c> configuration section. <see cref="TmnCode"/> and <see cref="HashSecret"/>
/// are issued by VNPay for your merchant; keep the secret out of source control (user-secrets / env).
/// <see cref="BaseUrl"/> is the sandbox pay URL by default — swap for the live URL in production.
/// </summary>
public sealed class VnPayOptions
{
    public const string SectionName = "VnPay";

    /// <summary>Merchant terminal code issued by VNPay.</summary>
    public string TmnCode { get; set; } = string.Empty;

    /// <summary>Merchant secret used to HMAC-SHA512 sign the request (and verify callbacks).</summary>
    public string HashSecret { get; set; } = string.Empty;

    /// <summary>VNPay pay entry point. Sandbox by default.</summary>
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary>Your endpoint VNPay redirects the customer back to after payment.</summary>
    public string ReturnUrl { get; set; } = string.Empty;

    public string Version { get; set; } = "2.1.0";
    public string Command { get; set; } = "pay";
    public string CurrCode { get; set; } = "VND";
    public string Locale { get; set; } = "vn";

    /// <summary>Minutes the payment link stays valid before VNPay expires it.</summary>
    public int ExpireMinutes { get; set; } = 15;
}
