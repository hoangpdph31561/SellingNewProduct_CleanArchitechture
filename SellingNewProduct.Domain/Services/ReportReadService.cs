using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the reporting read port; forwards to the read repository (no business rules).</summary>
public sealed class ReportReadService : IReportReadService
{
    private readonly IReportReadRepository myReportRepository;

    public ReportReadService(IReportReadRepository theReportRepository)
    {
        myReportRepository = theReportRepository;
    }

    public Task<PagedResult<BestSellingProductView>> GetBestSellingProductsAsync(int thePage, int thePageSize, CancellationToken theCancellationToken = default)
        => myReportRepository.GetBestSellingProductsAsync(thePage, thePageSize, theCancellationToken);

    public Task<IReadOnlyList<EmployeeSalesView>> GetEmployeeSalesLeaderboardAsync(CancellationToken theCancellationToken = default)
        => myReportRepository.GetEmployeeSalesLeaderboardAsync(theCancellationToken);

    public Task<IReadOnlyList<CategorySalesView>> GetSalesByCategoryAsync(CancellationToken theCancellationToken = default)
        => myReportRepository.GetSalesByCategoryAsync(theCancellationToken);

    public Task<IReadOnlyList<DailySalesView>> GetDailySalesAsync(DateTime? theFromUtc, DateTime? theToUtc, CancellationToken theCancellationToken = default)
        => myReportRepository.GetDailySalesAsync(theFromUtc, theToUtc, theCancellationToken);

    public Task<PagedResult<LowStockProductView>> GetLowStockProductsAsync(int theThreshold, int thePage, int thePageSize, CancellationToken theCancellationToken = default)
        => myReportRepository.GetLowStockProductsAsync(theThreshold, thePage, thePageSize, theCancellationToken);
}
