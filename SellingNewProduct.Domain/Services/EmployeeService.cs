using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Domain.Services;

internal sealed class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository myEmployeeRepository;
    private readonly IUserRepository myUserRepository;

    public EmployeeService(IEmployeeRepository theEmployeeRepository, IUserRepository theUserRepository)
    {
        myEmployeeRepository = theEmployeeRepository;
        myUserRepository = theUserRepository;
    }

    public Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myEmployeeRepository.GetAllAsync(theCancellationToken);

    public Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myEmployeeRepository.GetByIdAsync(theId, theCancellationToken);

    public async Task<Employee> CreateAsync(CreateEmployeeCommand theCommand, CancellationToken theCancellationToken = default)
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

    public Task<EmployeeSummaryView?> GetSummaryByIdAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default)
        => myEmployeeRepository.GetSummaryByIdAsync(theEmployeeId, theCancellationToken);

    public Task<PagedResult<EmployeeSummaryView>> SearchAsync(EmployeeSearchQuery theQuery, CancellationToken theCancellationToken = default)
        => myEmployeeRepository.SearchAsync(theQuery, theCancellationToken);
}
