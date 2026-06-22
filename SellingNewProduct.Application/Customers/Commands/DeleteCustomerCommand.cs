using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Customers;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest;

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

public sealed class DeleteCustomerCommandValidator : AbstractValidator<DeleteCustomerCommand>
{
    public DeleteCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
