using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Domain.Services;

/// <summary>
/// Reporting behavior implementation. Pure read-side: it forwards to <see cref="IReportRepository"/>.
/// <c>internal</c>: the outside world only sees <see cref="IReportService"/>.
/// </summary>
internal sealed class ReportService : IReportService
{
    private readonly IReportRepository myReportRepository;

    public ReportService(IReportRepository theReportRepository)
    {
        myReportRepository = theReportRepository;
    }

    public Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(int thePage = 1, int thePageSize = 10, CancellationToken theCancellationToken = default)
        => myReportRepository.GetBestSellingProductsAsync(thePage, thePageSize, theCancellationToken);

    public Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default)
        => myReportRepository.GetEmployeeSalesLeaderboardAsync(theCancellationToken);

    public Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default)
        => myReportRepository.GetSalesByCategoryAsync(theCancellationToken);

    public Task<IReadOnlyList<DailySalesView>> GetDailySalesAsync(DateTime? theFromUtc = null, DateTime? theToUtc = null, CancellationToken theCancellationToken = default)
        => myReportRepository.GetDailySalesAsync(theFromUtc, theToUtc, theCancellationToken);

    public Task<PagedResult<LowStockProductView>> GetLowStockProductsAsync(int theThreshold = 5, int thePage = 1, int thePageSize = PageRequest.DefaultPageSize, CancellationToken theCancellationToken = default)
        => myReportRepository.GetLowStockProductsAsync(theThreshold, thePage, thePageSize, theCancellationToken);
}
