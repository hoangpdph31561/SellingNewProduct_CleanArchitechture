using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Application.Customers;

// ----- Create customer ----------------------------------------------------------------------

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

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Customer>
{
    private readonly ICustomerWriteRepository myCustomerRepository;

    public CreateCustomerCommandHandler(ICustomerWriteRepository theCustomerRepository)
    {
        myCustomerRepository = theCustomerRepository;
    }

    public async Task<Customer> Handle(CreateCustomerCommand theCommand, CancellationToken theCancellationToken)
    {
        var aAddress = Address.Create(
            theCommand.Street, theCommand.Ward, theCommand.District, theCommand.City, theCommand.Country);

        var aCustomer = Customer.Create(
            theCommand.FullName,
            Email.Create(theCommand.Email),
            theCommand.PhoneNumber,
            aAddress,
            theCommand.UserId);

        await myCustomerRepository.AddAsync(aCustomer, theCancellationToken);
        return aCustomer;
    }
}

// ----- Delete customer ----------------------------------------------------------------------

public sealed record DeleteCustomerCommand(Guid Id) : IRequest;

public sealed class DeleteCustomerCommandValidator : AbstractValidator<DeleteCustomerCommand>
{
    public DeleteCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand>
{
    private readonly ICustomerWriteRepository myCustomerRepository;

    public DeleteCustomerCommandHandler(ICustomerWriteRepository theCustomerRepository)
    {
        myCustomerRepository = theCustomerRepository;
    }

    public Task Handle(DeleteCustomerCommand theCommand, CancellationToken theCancellationToken)
        => myCustomerRepository.DeleteAsync(theCommand.Id, theCancellationToken);
}
