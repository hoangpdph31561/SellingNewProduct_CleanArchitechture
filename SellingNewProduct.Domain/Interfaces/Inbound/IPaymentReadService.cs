using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for payment reads.</summary>
public interface IPaymentReadService : IReadService<Payment>
{
    Task<PagedResult<PaymentSummaryView>> SearchAsync(PaymentSearchQuery theCriteria, CancellationToken theCancellationToken = default);

    Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(int thePage, int thePageSize, CancellationToken theCancellationToken = default);
}
