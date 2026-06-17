using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Reporting behavior — the single entry point the API depends on for analytics. There is no
/// "Report" aggregate to mutate, so every method here is read-only and delegates to
/// <c>IReportRepository</c>. Kept as a service (not injected queries) so the API talks only to
/// services, consistent with every other module.
/// </summary>
public interface IReportService
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
