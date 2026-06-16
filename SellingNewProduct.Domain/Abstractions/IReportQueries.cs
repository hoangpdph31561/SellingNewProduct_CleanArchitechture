using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

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

    /// <summary>
    /// Revenue grouped by product category (quantity sold + revenue), best-selling
    /// category first. (SQL: JOIN OrderDetails x Products x Categories x Orders,
    /// GROUP BY category. Only Confirmed/Shipped orders count.)
    /// </summary>
    Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default);

    /// <summary>
    /// A daily revenue time series between <paramref name="theFromUtc"/> and
    /// <paramref name="theToUtc"/> (inclusive; null = no bound on that side): orders and
    /// total amount per calendar day, oldest day first. Only Confirmed/Shipped orders count.
    /// </summary>
    Task<IReadOnlyList<DailySalesView>> GetDailySalesAsync(
        DateTime? theFromUtc = null,
        DateTime? theToUtc = null,
        CancellationToken theCancellationToken = default);

    /// <summary>
    /// Products whose stock is at or below <paramref name="theThreshold"/> (default 5),
    /// with the category name — a restock-alert list, lowest stock first. Returns one page
    /// plus the total number of low-stock products.
    /// </summary>
    Task<PagedResult<LowStockProductView>> GetLowStockProductsAsync(
        int theThreshold = 5,
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default);
}
