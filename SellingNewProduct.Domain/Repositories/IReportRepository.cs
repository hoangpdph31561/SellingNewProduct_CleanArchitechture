using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Repositories;

/// <summary>
/// Reporting/analytics — JOIN + GROUP BY across several tables to produce aggregate numbers.
/// There is no "Report" aggregate, so this repository returns read models directly. It spans
/// many aggregates (orders, products, categories, employees), which is exactly why none of the
/// per-aggregate repositories owns these queries.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Best-selling products ranked by quantity sold (with category name), returned ONE
    /// page at a time plus the total number of distinct products sold. Page 1 is the classic "top N".
    /// </summary>
    Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default);

    /// <summary>Sales leaderboard per selling employee (total orders + total revenue).</summary>
    Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default);

    /// <summary>Revenue grouped by product category (quantity sold + revenue), best-selling category first.</summary>
    Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default);

    /// <summary>A daily revenue time series, optionally bounded by a date range (inclusive; null = no bound).</summary>
    Task<IReadOnlyList<DailySalesView>> GetDailySalesAsync(
        DateTime? theFromUtc = null,
        DateTime? theToUtc = null,
        CancellationToken theCancellationToken = default);

    /// <summary>Products at or below a stock threshold (default 5), with the category name, lowest stock first; one page plus the total count.</summary>
    Task<PagedResult<LowStockProductView>> GetLowStockProductsAsync(
        int theThreshold = 5,
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default);
}
