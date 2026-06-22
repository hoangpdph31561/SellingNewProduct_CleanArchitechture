using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Customers;

/// <summary>Write-side command: create a customer. Address is carried as flat fields.</summary>
public sealed record CreateCustomerCommand(
    string FullName,
    string Email,
    string PhoneNumber,
    string Street,
    string Ward,
    string District,
    string City,
    string Country,
    Guid? UserId) : IRequest<Customer>;

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

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
    }
}
