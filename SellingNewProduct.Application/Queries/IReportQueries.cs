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
    /// Top N best-selling products (by quantity sold), including the category name.
    /// (SQL: JOIN OrderDetails x Products x Categories x Orders, GROUP BY product.)
    /// </summary>
    Task<IReadOnlyList<BestSellingProductView>> GetBestSellingProductsAsync(int theTop, CancellationToken theCancellationToken = default);

    /// <summary>
    /// Sales leaderboard per selling employee (total orders + total revenue).
    /// (SQL: JOIN Orders x Employees, GROUP BY employee.)
    /// </summary>
    Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default);
}
