using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Implements the employee inbound port. Owns the rule that an employee must be linked to an
/// existing user account.
/// </summary>
public sealed class EmployeeWriteService : IEmployeeWriteService
{
    private readonly IEmployeeWriteRepository myEmployeeRepository;
    private readonly IUserWriteRepository myUserRepository;

    public EmployeeWriteService(
        IEmployeeWriteRepository theEmployeeRepository,
        IUserWriteRepository theUserRepository)
    {
        myEmployeeRepository = theEmployeeRepository;
        myUserRepository = theUserRepository;
    }

    public async Task<Employee> CreateAsync(
        string theFullName,
        string thePosition,
        DateTime theHireDate,
        Guid theUserId,
        CancellationToken theCancellationToken = default)
    {
        // An employee must be linked to an existing user account.
        var aUser = await myUserRepository.GetByIdAsync(theUserId, theCancellationToken);
        if (aUser is null)
        {
            throw new NotFoundException($"User '{theUserId}' not found.");
        }

        var aEmployee = Employee.Create(theFullName, thePosition, theHireDate, theUserId);
        await myEmployeeRepository.AddAsync(aEmployee, theCancellationToken);
        return aEmployee;
    }
}
