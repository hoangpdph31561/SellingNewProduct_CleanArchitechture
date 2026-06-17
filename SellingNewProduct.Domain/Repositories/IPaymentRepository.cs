using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Payment>> GetByOrderAsync(Guid theOrderId, CancellationToken theCancellationToken = default);

    Task AddAsync(Payment thePayment, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Payment thePayment, CancellationToken theCancellationToken = default);

    // Read side: payment search + the "outstanding orders" report (read models, not the aggregate).
    Task<PagedResult<PaymentSummaryView>> SearchAsync(PaymentSearchQuery theQuery, CancellationToken theCancellationToken = default);

    Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(int thePage = 1, int thePageSize = PageRequest.DefaultPageSize, CancellationToken theCancellationToken = default);
}
