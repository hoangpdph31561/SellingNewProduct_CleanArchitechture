using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Outbound;

/// <summary>
/// Reporting/analytics — JOIN + GROUP BY across several tables to produce aggregate numbers.
/// There is no "Report" aggregate, so this repository returns read models directly. Pure read
/// side; it spans many aggregates (orders, products, categories, employees).
/// </summary>
public interface IReportReadRepository
{
    Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<DailySalesView>> GetDailySalesAsync(
        DateTime? theFromUtc = null,
        DateTime? theToUtc = null,
        CancellationToken theCancellationToken = default);

    Task<PagedResult<LowStockProductView>> GetLowStockProductsAsync(
        int theThreshold = 5,
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default);
}
