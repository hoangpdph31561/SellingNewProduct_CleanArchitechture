using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Payments;

public sealed record SearchPaymentsQuery(PaymentSearchQuery Criteria) : IRequest<PagedResult<PaymentSummaryView>>;

public sealed class SearchPaymentsQueryHandler : IRequestHandler<SearchPaymentsQuery, PagedResult<PaymentSummaryView>>
{
    private readonly IPaymentReadService myPaymentReadService;

    public SearchPaymentsQueryHandler(IPaymentReadService thePaymentReadService)
    {
        myPaymentReadService = thePaymentReadService;
    }

    public Task<PagedResult<PaymentSummaryView>> Handle(SearchPaymentsQuery theQuery, CancellationToken theCancellationToken)
        => myPaymentReadService.SearchAsync(theQuery.Criteria, theCancellationToken);
}
