using MediatR;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Customers;

public sealed class GetAllCustomersQueryHandler : IRequestHandler<GetAllCustomersQuery, IReadOnlyList<Customer>>
{
    private readonly ICustomerReadService myCustomerReadService;

    public GetAllCustomersQueryHandler(ICustomerReadService theCustomerReadService)
    {
        myCustomerReadService = theCustomerReadService;
    }

    public Task<IReadOnlyList<Customer>> Handle(GetAllCustomersQuery theQuery, CancellationToken theCancellationToken)
        => myCustomerReadService.GetAllAsync(theCancellationToken);
}
