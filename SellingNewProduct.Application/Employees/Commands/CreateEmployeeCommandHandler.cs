using MediatR;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Interfaces.Inbound;

namespace SellingNewProduct.Application.Employees;

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
