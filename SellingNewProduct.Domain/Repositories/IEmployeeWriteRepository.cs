using SellingNewProduct.Domain.Employees;

namespace SellingNewProduct.Domain.Repositories;

public interface IEmployeeWriteRepository
{
    /// <summary>Loads an employee aggregate (used by the order flow "employee must exist").</summary>
    Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task AddAsync(Employee theEmployee, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Employee theEmployee, CancellationToken theCancellationToken = default);
}
