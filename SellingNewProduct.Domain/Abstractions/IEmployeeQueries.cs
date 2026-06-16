using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Read side for employees — kept separate from <c>IEmployeeRepository</c> (write side).
/// Each row is enriched with the number of real-sale orders the employee handled, so a
/// list screen can show productivity without loading every Order aggregate.
/// </summary>
public interface IEmployeeQueries
{
    /// <summary>One enriched employee row for display, or <c>null</c> if not found.</summary>
    Task<EmployeeSummaryView?> GetByIdAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Search/filter employees for a list screen — see <see cref="EmployeeSearchQuery"/> for
    /// the filters, paging and sorting. Each row carries the number of real-sale orders the
    /// employee handled. Returns one page plus the total count.
    /// </summary>
    Task<PagedResult<EmployeeSummaryView>> SearchAsync(EmployeeSearchQuery theQuery, CancellationToken theCancellationToken = default);
}
