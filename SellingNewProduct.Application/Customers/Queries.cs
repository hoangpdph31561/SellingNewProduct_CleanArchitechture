using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Application.Customers;

public sealed record GetAllCustomersQuery : IRequest<IReadOnlyList<Customer>>;

public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Customer?>;

public sealed record SearchCustomersQuery(CustomerSearchQuery Criteria) : IRequest<PagedResult<CustomerSummaryView>>;

public sealed record GetTopCustomersQuery(int Page, int PageSize) : IRequest<PagedResult<TopCustomerView>>;

public sealed class CustomerQueryHandlers :
    IRequestHandler<GetAllCustomersQuery, IReadOnlyList<Customer>>,
    IRequestHandler<GetCustomerByIdQuery, Customer?>,
    IRequestHandler<SearchCustomersQuery, PagedResult<CustomerSummaryView>>,
    IRequestHandler<GetTopCustomersQuery, PagedResult<TopCustomerView>>
{
    private readonly ICustomerReadRepository myCustomerRepository;

    public CustomerQueryHandlers(ICustomerReadRepository theCustomerRepository)
    {
        myCustomerRepository = theCustomerRepository;
    }

    public Task<IReadOnlyList<Customer>> Handle(GetAllCustomersQuery theQuery, CancellationToken theCancellationToken)
        => myCustomerRepository.GetAllAsync(theCancellationToken);

    public Task<Customer?> Handle(GetCustomerByIdQuery theQuery, CancellationToken theCancellationToken)
        => myCustomerRepository.GetByIdAsync(theQuery.Id, theCancellationToken);

    public Task<PagedResult<CustomerSummaryView>> Handle(SearchCustomersQuery theQuery, CancellationToken theCancellationToken)
        => myCustomerRepository.SearchAsync(theQuery.Criteria, theCancellationToken);

    public Task<PagedResult<TopCustomerView>> Handle(GetTopCustomersQuery theQuery, CancellationToken theCancellationToken)
        => myCustomerRepository.GetTopCustomersAsync(theQuery.Page, theQuery.PageSize, theCancellationToken);
}
