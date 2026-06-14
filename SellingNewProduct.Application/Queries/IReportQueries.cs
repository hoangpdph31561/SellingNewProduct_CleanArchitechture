using SellingNewProduct.Application.Common;
using SellingNewProduct.Application.ReadModels;

namespace SellingNewProduct.Application.Queries;

/// <summary>
/// Reporting/analytics queries — essentially JOIN + GROUP BY across several tables
/// to produce aggregate numbers. These are the classic example of what does NOT
/// belong on an aggregate: "best-selling products" and "sales per employee" are
/// query results, not properties of any single entity.
/// </summary>
public interface IReportQueries
{
    /// <summary>
    /// Best-selling products ranked by quantity sold (with category name), returned ONE
    /// page at a time plus the total number of distinct products sold. Page 1 is therefore
    /// the classic "top N". (SQL: JOIN OrderDetails x Products x Categories x Orders,
    /// GROUP BY product.) <paramref name="thePage"/> is 1-based; page/pageSize are clamped
    /// (see <see cref="PageRequest"/>).
    /// </summary>
    Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default);

    /// <summary>
    /// Sales leaderboard per selling employee (total orders + total revenue).
    /// (SQL: JOIN Orders x Employees, GROUP BY employee.)
    /// </summary>
    Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default);
}
