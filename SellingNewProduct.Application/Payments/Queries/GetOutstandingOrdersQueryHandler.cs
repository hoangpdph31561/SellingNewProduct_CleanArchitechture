using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Payments;

public sealed class GetOutstandingOrdersQueryHandler : IRequestHandler<GetOutstandingOrdersQuery, PagedResult<OutstandingOrderView>>
{
    private readonly IPaymentReadService myPaymentReadService;

    public GetOutstandingOrdersQueryHandler(IPaymentReadService thePaymentReadService)
    {
        myPaymentReadService = thePaymentReadService;
    }

    public Task<PagedResult<OutstandingOrderView>> Handle(GetOutstandingOrdersQuery theQuery, CancellationToken theCancellationToken)
        => myPaymentReadService.GetOutstandingOrdersAsync(theQuery.Page, theQuery.PageSize, theCancellationToken);
}
