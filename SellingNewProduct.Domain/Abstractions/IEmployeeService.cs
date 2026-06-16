using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Employees;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>Employee write-side behavior. The API depends on this, not on the repositories.</summary>
public interface IEmployeeService
{
    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Employee> CreateAsync(CreateEmployeeCommand theCommand, CancellationToken theCancellationToken = default);
}
