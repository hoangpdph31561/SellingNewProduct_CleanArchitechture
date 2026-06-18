using MediatR;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;

namespace SellingNewProduct.Application.Reports;

// Reporting is pure read side: every query forwards to IReportReadRepository.

public sealed record GetBestSellingProductsQuery(int Page, int PageSize) : IRequest<PagedResult<BestSellingProductView>>;

public sealed record GetEmployeeSalesLeaderboardQuery : IRequest<IReadOnlyList<EmployeeSalesView>>;

public sealed record GetSalesByCategoryQuery : IRequest<IReadOnlyList<CategorySalesView>>;

public sealed record GetDailySalesQuery(DateTime? FromUtc, DateTime? ToUtc) : IRequest<IReadOnlyList<DailySalesView>>;

public sealed record GetLowStockProductsQuery(int Threshold, int Page, int PageSize) : IRequest<PagedResult<LowStockProductView>>;

public sealed class ReportQueryHandlers :
    IRequestHandler<GetBestSellingProductsQuery, PagedResult<BestSellingProductView>>,
    IRequestHandler<GetEmployeeSalesLeaderboardQuery, IReadOnlyList<EmployeeSalesView>>,
    IRequestHandler<GetSalesByCategoryQuery, IReadOnlyList<CategorySalesView>>,
    IRequestHandler<GetDailySalesQuery, IReadOnlyList<DailySalesView>>,
    IRequestHandler<GetLowStockProductsQuery, PagedResult<LowStockProductView>>
{
    private readonly IReportReadRepository myReportRepository;

    public ReportQueryHandlers(IReportReadRepository theReportRepository)
    {
        myReportRepository = theReportRepository;
    }

    public Task<PagedResult<BestSellingProductView>> Handle(GetBestSellingProductsQuery theQuery, CancellationToken theCancellationToken)
        => myReportRepository.GetBestSellingProductsAsync(theQuery.Page, theQuery.PageSize, theCancellationToken);

    public Task<IReadOnlyList<EmployeeSalesView>> Handle(GetEmployeeSalesLeaderboardQuery theQuery, CancellationToken theCancellationToken)
        => myReportRepository.GetEmployeeSalesLeaderboardAsync(theCancellationToken);

    public Task<IReadOnlyList<CategorySalesView>> Handle(GetSalesByCategoryQuery theQuery, CancellationToken theCancellationToken)
        => myReportRepository.GetSalesByCategoryAsync(theCancellationToken);

    public Task<IReadOnlyList<DailySalesView>> Handle(GetDailySalesQuery theQuery, CancellationToken theCancellationToken)
        => myReportRepository.GetDailySalesAsync(theQuery.FromUtc, theQuery.ToUtc, theCancellationToken);

    public Task<PagedResult<LowStockProductView>> Handle(GetLowStockProductsQuery theQuery, CancellationToken theCancellationToken)
        => myReportRepository.GetLowStockProductsAsync(theQuery.Threshold, theQuery.Page, theQuery.PageSize, theCancellationToken);
}
