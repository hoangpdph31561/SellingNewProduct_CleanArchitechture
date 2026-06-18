using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

public interface IEmployeeReadRepository
{
    Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<EmployeeSummaryView?> GetSummaryByIdAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default);

    Task<PagedResult<EmployeeSummaryView>> SearchAsync(EmployeeSearchQuery theQuery, CancellationToken theCancellationToken = default);
}
