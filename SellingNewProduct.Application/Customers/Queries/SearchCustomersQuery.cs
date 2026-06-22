using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Customers;

public sealed record SearchCustomersQuery(CustomerSearchQuery Criteria) : IRequest<PagedResult<CustomerSummaryView>>;

public sealed class SearchCustomersQueryHandler : IRequestHandler<SearchCustomersQuery, PagedResult<CustomerSummaryView>>
{
    private readonly ICustomerReadService myCustomerReadService;

    public SearchCustomersQueryHandler(ICustomerReadService theCustomerReadService)
    {
        myCustomerReadService = theCustomerReadService;
    }

    public Task<PagedResult<CustomerSummaryView>> Handle(SearchCustomersQuery theQuery, CancellationToken theCancellationToken)
        => myCustomerReadService.SearchAsync(theQuery.Criteria, theCancellationToken);
}
