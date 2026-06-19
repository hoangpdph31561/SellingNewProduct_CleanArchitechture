using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for employee reads.</summary>
public interface IEmployeeReadService : IReadService<Employee>
{
    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<PagedResult<EmployeeSummaryView>> SearchAsync(EmployeeSearchQuery theCriteria, CancellationToken theCancellationToken = default);
}
