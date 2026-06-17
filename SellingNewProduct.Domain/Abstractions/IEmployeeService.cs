using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Employee behavior — the single entry point the API depends on for both the write side
/// (aggregate operations) and the read side (list/search projections enriched with order counts).
/// </summary>
public interface IEmployeeService
{
    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Employee> CreateAsync(CreateEmployeeCommand theCommand, CancellationToken theCancellationToken = default);

    /// <summary>One enriched employee row, or <c>null</c> — the flat read-side counterpart of <see cref="GetByIdAsync"/>.</summary>
    Task<EmployeeSummaryView?> GetSummaryByIdAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default);

    /// <summary>Search/filter employees (see <see cref="EmployeeSearchQuery"/>), one page plus the total count.</summary>
    Task<PagedResult<EmployeeSummaryView>> SearchAsync(EmployeeSearchQuery theQuery, CancellationToken theCancellationToken = default);
}
