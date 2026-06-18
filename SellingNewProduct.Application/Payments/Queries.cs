using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Application.Payments;

public sealed record GetPaymentByIdQuery(Guid Id) : IRequest<Payment?>;

public sealed record SearchPaymentsQuery(PaymentSearchQuery Criteria) : IRequest<PagedResult<PaymentSummaryView>>;

public sealed record GetOutstandingOrdersQuery(int Page, int PageSize) : IRequest<PagedResult<OutstandingOrderView>>;

public sealed class PaymentQueryHandlers :
    IRequestHandler<GetPaymentByIdQuery, Payment?>,
    IRequestHandler<SearchPaymentsQuery, PagedResult<PaymentSummaryView>>,
    IRequestHandler<GetOutstandingOrdersQuery, PagedResult<OutstandingOrderView>>
{
    private readonly IPaymentReadRepository myPaymentRepository;

    public PaymentQueryHandlers(IPaymentReadRepository thePaymentRepository)
    {
        myPaymentRepository = thePaymentRepository;
    }

    public Task<Payment?> Handle(GetPaymentByIdQuery theQuery, CancellationToken theCancellationToken)
        => myPaymentRepository.GetByIdAsync(theQuery.Id, theCancellationToken);

    public Task<PagedResult<PaymentSummaryView>> Handle(SearchPaymentsQuery theQuery, CancellationToken theCancellationToken)
        => myPaymentRepository.SearchAsync(theQuery.Criteria, theCancellationToken);

    public Task<PagedResult<OutstandingOrderView>> Handle(GetOutstandingOrdersQuery theQuery, CancellationToken theCancellationToken)
        => myPaymentRepository.GetOutstandingOrdersAsync(theQuery.Page, theQuery.PageSize, theCancellationToken);
}
