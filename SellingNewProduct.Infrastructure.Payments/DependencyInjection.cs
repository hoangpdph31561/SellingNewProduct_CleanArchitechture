using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Payments.VnPay;

namespace SellingNewProduct.Infrastructure.Payments;

/// <summary>
/// Composition for the payment gateway. Binds the <c>VnPay</c> configuration section and wires the
/// VNPay adapter as the <see cref="IPaymentGateway"/> the order/payment flow uses. Register from the
/// composition root: <c>services.AddVnPayPayments(configuration);</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddVnPayPayments(this IServiceCollection theServices, IConfiguration theConfiguration)
    {
        theServices.Configure<VnPayOptions>(theConfiguration.GetSection(VnPayOptions.SectionName));
        theServices.AddSingleton<IPaymentGateway, VnPayPaymentGateway>();
        return theServices;
    }
}
