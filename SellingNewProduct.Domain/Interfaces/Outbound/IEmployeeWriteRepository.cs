using SellingNewProduct.Domain.Employees;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

public interface IEmployeeWriteRepository : IWriteRepository<Employee>
{
    /// <summary>Loads an employee aggregate (used by the order flow "employee must exist").</summary>
    Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

}
