using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SellingNewProduct.Domain.Interfaces.Outbound;

namespace SellingNewProduct.Infrastructure.Payments.VnPay;

/// <summary>
/// VNPay adapter for <see cref="IPaymentGateway"/>. Implements the redirect model:
///   1. <see cref="CreatePayment"/> builds the sorted, URL-encoded parameter list, signs it with
///      HMAC-SHA512 over the merchant secret, and returns the pay URL to redirect the customer to.
///   2. <see cref="VerifyCallback"/> recomputes the signature over the returned parameters and compares
///      it to <c>vnp_SecureHash</c> — this is what proves the callback really came from VNPay and was
///      not tampered with. Only then is <c>vnp_ResponseCode == "00"</c> trusted as "paid".
/// Both operations are local computation (no outbound HTTP), so no circuit breaker is involved.
/// </summary>
public sealed class VnPayPaymentGateway : IPaymentGateway
{
    // VNPay timestamps are in Vietnam time (GMT+7), formatted yyyyMMddHHmmss.
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private readonly VnPayOptions myOptions;
    private readonly ILogger<VnPayPaymentGateway> myLogger;

    public VnPayPaymentGateway(IOptions<VnPayOptions> theOptions, ILogger<VnPayPaymentGateway> theLogger)
    {
        myOptions = theOptions.Value;
        myLogger = theLogger;
    }

    public PaymentGatewayResult CreatePayment(PaymentGatewayRequest theRequest)
    {
        var aNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

        // VNPay wants the amount in the smallest unit (VND × 100) as an integer, no decimals.
        var aAmount = (long)Math.Round(theRequest.Amount * 100m, MidpointRounding.AwayFromZero);

        // Ordinal sort is required so both sides compute the SAME signed string.
        var aParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = myOptions.Version,
            ["vnp_Command"] = myOptions.Command,
            ["vnp_TmnCode"] = myOptions.TmnCode,
            ["vnp_Amount"] = aAmount.ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = myOptions.CurrCode,
            ["vnp_TxnRef"] = theRequest.OrderId.ToString("N"),
            ["vnp_OrderInfo"] = theRequest.OrderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = myOptions.Locale,
            ["vnp_ReturnUrl"] = myOptions.ReturnUrl,
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(theRequest.ClientIpAddress) ? "127.0.0.1" : theRequest.ClientIpAddress,
            ["vnp_CreateDate"] = aNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = aNow.AddMinutes(myOptions.ExpireMinutes).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        };

        var aSignData = BuildEncodedQuery(aParameters);
        var aSecureHash = HmacSha512(myOptions.HashSecret, aSignData);
        var aPayUrl = $"{myOptions.BaseUrl}?{aSignData}&vnp_SecureHash={aSecureHash}";

        myLogger.LogInformation("💳 VNPay ▶ created pay URL for order {OrderId} ({Amount:N0} {Currency}).",
            theRequest.OrderId, theRequest.Amount, theRequest.Currency);

        return new PaymentGatewayResult(aPayUrl);
    }

    public PaymentCallbackResult VerifyCallback(IReadOnlyDictionary<string, string> theCallbackParameters)
    {
        var aReceivedHash = theCallbackParameters.GetValueOrDefault("vnp_SecureHash", string.Empty);

        // Sign every vnp_* parameter EXCEPT the two hash fields, then compare.
        var aToSign = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var aPair in theCallbackParameters)
        {
            if (aPair.Key.StartsWith("vnp_", StringComparison.Ordinal)
                && aPair.Key is not ("vnp_SecureHash" or "vnp_SecureHashType"))
            {
                aToSign[aPair.Key] = aPair.Value;
            }
        }

        var aExpectedHash = HmacSha512(myOptions.HashSecret, BuildEncodedQuery(aToSign));
        var aIsValid = !string.IsNullOrEmpty(aReceivedHash)
            && string.Equals(aExpectedHash, aReceivedHash, StringComparison.OrdinalIgnoreCase);

        var aResponseCode = theCallbackParameters.GetValueOrDefault("vnp_ResponseCode", string.Empty);
        var aTransactionStatus = theCallbackParameters.GetValueOrDefault("vnp_TransactionStatus", string.Empty);
        var aIsSuccessful = aIsValid && aResponseCode == "00" && aTransactionStatus == "00";

        Guid.TryParseExact(theCallbackParameters.GetValueOrDefault("vnp_TxnRef", string.Empty), "N", out var aOrderId);

        var aAmount = 0m;
        if (long.TryParse(theCallbackParameters.GetValueOrDefault("vnp_Amount", "0"),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var aRawAmount))
        {
            aAmount = aRawAmount / 100m;
        }

        if (!aIsValid)
        {
            myLogger.LogWarning("💳 VNPay ✖ callback signature INVALID for TxnRef {TxnRef} — rejecting.",
                theCallbackParameters.GetValueOrDefault("vnp_TxnRef", "(none)"));
        }

        return new PaymentCallbackResult(
            aIsValid,
            aIsSuccessful,
            aOrderId,
            aAmount,
            theCallbackParameters.GetValueOrDefault("vnp_TransactionNo", string.Empty),
            aResponseCode);
    }

    /// <summary>Joins the (already sorted) parameters into a URL-encoded <c>k=v&amp;k=v</c> string.
    /// The SAME encoding must be used for the signed data and the redirect query, or the hash won't match.</summary>
    private static string BuildEncodedQuery(SortedDictionary<string, string> theParameters)
    {
        var aBuilder = new StringBuilder();
        foreach (var aPair in theParameters)
        {
            if (string.IsNullOrEmpty(aPair.Value))
            {
                continue;
            }

            if (aBuilder.Length > 0)
            {
                aBuilder.Append('&');
            }

            aBuilder.Append(WebUtility.UrlEncode(aPair.Key));
            aBuilder.Append('=');
            aBuilder.Append(WebUtility.UrlEncode(aPair.Value));
        }

        return aBuilder.ToString();
    }

    private static string HmacSha512(string theKey, string theData)
    {
        using var aHmac = new HMACSHA512(Encoding.UTF8.GetBytes(theKey));
        var aHashBytes = aHmac.ComputeHash(Encoding.UTF8.GetBytes(theData));
        return Convert.ToHexStringLower(aHashBytes);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        // Windows uses "SE Asia Standard Time"; Linux/macOS use the IANA id. Fall back to a fixed +7.
        foreach (var aId in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(aId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next id.
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("VN+7", TimeSpan.FromHours(7), "Vietnam (+7)", "Vietnam (+7)");
    }
}
