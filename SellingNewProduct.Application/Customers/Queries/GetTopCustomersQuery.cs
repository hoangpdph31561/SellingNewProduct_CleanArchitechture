using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Customers;

public sealed record GetTopCustomersQuery(int Page, int PageSize) : IRequest<PagedResult<TopCustomerView>>;

public sealed class GetTopCustomersQueryHandler : IRequestHandler<GetTopCustomersQuery, PagedResult<TopCustomerView>>
{
    private readonly ICustomerReadService myCustomerReadService;

    public GetTopCustomersQueryHandler(ICustomerReadService theCustomerReadService)
    {
        myCustomerReadService = theCustomerReadService;
    }

    public Task<PagedResult<TopCustomerView>> Handle(GetTopCustomersQuery theQuery, CancellationToken theCancellationToken)
        => myCustomerReadService.GetTopCustomersAsync(theQuery.Page, theQuery.PageSize, theCancellationToken);
}
