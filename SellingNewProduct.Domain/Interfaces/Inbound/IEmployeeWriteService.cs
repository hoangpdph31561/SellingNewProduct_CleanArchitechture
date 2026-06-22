using SellingNewProduct.Domain.Employees;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for employee writes.</summary>
public interface IEmployeeWriteService : IWriteService<Employee>
{
    /// <summary>Creates an employee after checking the linked user account exists.</summary>
    Task<Employee> CreateAsync(
        string theFullName,
        string thePosition,
        DateTime theHireDate,
        Guid theUserId,
        CancellationToken theCancellationToken = default);
}
