using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the payment read port; forwards to the read repository (no business rules).</summary>
public sealed class PaymentReadService : IPaymentReadService
{
    private readonly IPaymentReadRepository myPaymentRepository;

    public PaymentReadService(IPaymentReadRepository thePaymentRepository)
    {
        myPaymentRepository = thePaymentRepository;
    }

    public Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myPaymentRepository.GetByIdAsync(theId, theCancellationToken);

    public Task<PagedResult<PaymentSummaryView>> SearchAsync(PaymentSearchQuery theCriteria, CancellationToken theCancellationToken = default)
        => myPaymentRepository.SearchAsync(theCriteria, theCancellationToken);

    public Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(int thePage, int thePageSize, CancellationToken theCancellationToken = default)
        => myPaymentRepository.GetOutstandingOrdersAsync(thePage, thePageSize, theCancellationToken);
}
