using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task AddAsync(Employee theEmployee, CancellationToken theCancellationToken = default);

    Task UpdateAsync(Employee theEmployee, CancellationToken theCancellationToken = default);

    // Read side: flat employee rows enriched with the real-sale order count (read models).
    Task<EmployeeSummaryView?> GetSummaryByIdAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default);

    Task<PagedResult<EmployeeSummaryView>> SearchAsync(EmployeeSearchQuery theQuery, CancellationToken theCancellationToken = default);
}
