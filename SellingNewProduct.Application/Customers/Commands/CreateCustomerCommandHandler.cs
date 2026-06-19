using MediatR;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Customers;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Customer>
{
    private readonly ICustomerWriteService myCustomerWriteService;

    public CreateCustomerCommandHandler(ICustomerWriteService theCustomerWriteService)
    {
        myCustomerWriteService = theCustomerWriteService;
    }

    public Task<Customer> Handle(CreateCustomerCommand theCommand, CancellationToken theCancellationToken)
    {
        var aRequest = new NewCustomer(
            theCommand.FullName,
            theCommand.Email,
            theCommand.PhoneNumber,
            theCommand.Street,
            theCommand.Ward,
            theCommand.District,
            theCommand.City,
            theCommand.Country,
            theCommand.UserId);

        return myCustomerWriteService.CreateAsync(aRequest, theCancellationToken);
    }
}
