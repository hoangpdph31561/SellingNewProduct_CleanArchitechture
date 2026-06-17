using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Payment behavior — the single entry point the API depends on for both the write side
/// (aggregate operations) and the read side (payment search + outstanding-orders report).
/// </summary>
public interface IPaymentService
{
    Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Payment> CreateAsync(CreatePaymentCommand theCommand, CancellationToken theCancellationToken = default);

    Task<Payment> CompleteAsync(Guid thePaymentId, CancellationToken theCancellationToken = default);

    /// <summary>Search/filter payments (see <see cref="PaymentSearchQuery"/>), one page plus the total count.</summary>
    Task<PagedResult<PaymentSummaryView>> SearchAsync(PaymentSearchQuery theQuery, CancellationToken theCancellationToken = default);

    /// <summary>Orders not yet fully paid (total minus completed payments), biggest balance first; one page plus the total count.</summary>
    Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(int thePage = 1, int thePageSize = PageRequest.DefaultPageSize, CancellationToken theCancellationToken = default);
}
