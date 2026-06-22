using MediatR;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Customers;

public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<Customer?>;

public sealed class GetCustomerByIdQueryHandler : IRequestHandler<GetCustomerByIdQuery, Customer?>
{
    private readonly ICustomerReadService myCustomerReadService;

    public GetCustomerByIdQueryHandler(ICustomerReadService theCustomerReadService)
    {
        myCustomerReadService = theCustomerReadService;
    }

    public Task<Customer?> Handle(GetCustomerByIdQuery theQuery, CancellationToken theCancellationToken)
        => myCustomerReadService.GetByIdAsync(theQuery.Id, theCancellationToken);
}
