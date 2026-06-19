using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Orders;

public sealed class GetCustomerOrderHistoryQueryHandler : IRequestHandler<GetCustomerOrderHistoryQuery, CustomerOrderHistoryView?>
{
    private readonly IOrderReadService myOrderReadService;

    public GetCustomerOrderHistoryQueryHandler(IOrderReadService theOrderReadService)
    {
        myOrderReadService = theOrderReadService;
    }

    public Task<CustomerOrderHistoryView?> Handle(GetCustomerOrderHistoryQuery theQuery, CancellationToken theCancellationToken)
        => myOrderReadService.GetCustomerHistoryAsync(theQuery.CustomerId, theCancellationToken);
}
