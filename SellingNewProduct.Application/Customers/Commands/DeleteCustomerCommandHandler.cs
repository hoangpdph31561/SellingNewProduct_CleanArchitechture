using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Customers;

public sealed class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand>
{
    private readonly ICustomerWriteService myCustomerWriteService;

    public DeleteCustomerCommandHandler(ICustomerWriteService theCustomerWriteService)
    {
        myCustomerWriteService = theCustomerWriteService;
    }

    public Task Handle(DeleteCustomerCommand theCommand, CancellationToken theCancellationToken)
        => myCustomerWriteService.DeleteAsync(theCommand.Id, theCancellationToken);
}
