using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface IPaymentReadRepository
{
    Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<PagedResult<PaymentSummaryView>> SearchAsync(PaymentSearchQuery theQuery, CancellationToken theCancellationToken = default);

    Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(int thePage = 1, int thePageSize = PageRequest.DefaultPageSize, CancellationToken theCancellationToken = default);
}
