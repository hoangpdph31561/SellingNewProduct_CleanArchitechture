using SellingNewProduct.Domain.Employees;

namespace SellingNewProduct.Domain.Repositories;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task AddAsync(Employee theEmployee, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Employee theEmployee, CancellationToken theCancellationToken = default);
}
