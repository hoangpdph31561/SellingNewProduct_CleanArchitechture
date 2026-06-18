using FluentValidation;
using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Application.Employees;

/// <summary>Write-side command: create an employee linked to an existing user account.</summary>
public sealed record CreateEmployeeCommand(
    string FullName,
    string Position,
    DateTime HireDate,
    Guid UserId) : IRequest<Employee>;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Position).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Employee>
{
    private readonly IEmployeeWriteRepository myEmployeeRepository;
    private readonly IUserWriteRepository myUserRepository;

    public CreateEmployeeCommandHandler(
        IEmployeeWriteRepository theEmployeeRepository,
        IUserWriteRepository theUserRepository)
    {
        myEmployeeRepository = theEmployeeRepository;
        myUserRepository = theUserRepository;
    }

    public async Task<Employee> Handle(CreateEmployeeCommand theCommand, CancellationToken theCancellationToken)
    {
        // An employee must be linked to an existing user account.
        var aUser = await myUserRepository.GetByIdAsync(theCommand.UserId, theCancellationToken);
        if (aUser is null)
        {
            throw new NotFoundException($"User '{theCommand.UserId}' not found.");
        }

        var aEmployee = Employee.Create(theCommand.FullName, theCommand.Position, theCommand.HireDate, theCommand.UserId);
        await myEmployeeRepository.AddAsync(aEmployee, theCancellationToken);
        return aEmployee;
    }
}
