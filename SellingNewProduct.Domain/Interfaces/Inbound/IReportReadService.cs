using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for reporting reads.</summary>
public interface IReportReadService
{
    Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(int thePage, int thePageSize, CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<DailySalesView>> GetDailySalesAsync(DateTime? theFromUtc, DateTime? theToUtc, CancellationToken theCancellationToken = default);

    Task<PagedResult<LowStockProductView>> GetLowStockProductsAsync(int theThreshold, int thePage, int thePageSize, CancellationToken theCancellationToken = default);
}
