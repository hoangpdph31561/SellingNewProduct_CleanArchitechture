using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Employees;

/// <summary>Write-side command: create an employee linked to an existing user account.</summary>
public sealed record CreateEmployeeCommand(
    string FullName,
    string Position,
    DateTime HireDate,
    Guid UserId) : IRequest<Employee>;

public sealed class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Employee>
{
    private readonly IEmployeeWriteService myEmployeeWriteService;

    public CreateEmployeeCommandHandler(IEmployeeWriteService theEmployeeWriteService)
    {
        myEmployeeWriteService = theEmployeeWriteService;
    }

    public Task<Employee> Handle(CreateEmployeeCommand theCommand, CancellationToken theCancellationToken) =>
        myEmployeeWriteService.CreateAsync(
            theCommand.FullName, theCommand.Position, theCommand.HireDate, theCommand.UserId, theCancellationToken);
}

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Position).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserId).NotEmpty();
    }
}
