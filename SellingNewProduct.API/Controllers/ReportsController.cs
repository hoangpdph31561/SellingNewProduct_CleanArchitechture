using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

/// <summary>
/// Reporting endpoints — all backed by <see cref="IReportService"/>: JOIN + GROUP BY across
/// several tables to produce aggregate numbers, bypassing the domain aggregate.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService myReportService;

    public ReportsController(IReportService theReportService)
    {
        myReportService = theReportService;
    }

    /// <summary>
    /// Best-selling products ranked by quantity (with category name), paginated.
    /// Page 1 is the classic "top N". E.g. <c>GET /api/reports/best-selling-products?thePage=1&amp;thePageSize=5</c>
    /// </summary>
    [HttpGet("best-selling-products")]
    public async Task<ActionResult<PagedResult<BestSellingProductView>>> BestSellingProducts(
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myReportService.GetBestSellingProductsAsync(thePage, thePageSize, theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>Sales leaderboard per selling employee.</summary>
    [HttpGet("employee-sales")]
    public async Task<ActionResult<IReadOnlyList<EmployeeSalesView>>> EmployeeSales(CancellationToken theCancellationToken)
    {
        var aResult = await myReportService.GetEmployeeSalesLeaderboardAsync(theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>Revenue grouped by product category, best-selling category first.</summary>
    [HttpGet("sales-by-category")]
    public async Task<ActionResult<IReadOnlyList<CategorySalesView>>> SalesByCategory(CancellationToken theCancellationToken)
    {
        var aResult = await myReportService.GetSalesByCategoryAsync(theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>
    /// Daily revenue time series, optionally bounded by a date range.
    /// <c>GET /api/reports/daily-sales?theFromUtc=2026-01-01&amp;theToUtc=2026-06-30</c>
    /// </summary>
    [HttpGet("daily-sales")]
    public async Task<ActionResult<IReadOnlyList<DailySalesView>>> DailySales(
        [FromQuery] DateTime? theFromUtc,
        [FromQuery] DateTime? theToUtc,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myReportService.GetDailySalesAsync(theFromUtc, theToUtc, theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>
    /// Products at or below a stock threshold (default 5), lowest stock first, paginated.
    /// <c>GET /api/reports/low-stock-products?theThreshold=5&amp;thePage=1&amp;thePageSize=20</c>
    /// </summary>
    [HttpGet("low-stock-products")]
    public async Task<ActionResult<PagedResult<LowStockProductView>>> LowStockProducts(
        [FromQuery] int theThreshold = 5,
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myReportService.GetLowStockProductsAsync(theThreshold, thePage, thePageSize, theCancellationToken);
        return Ok(aResult);
    }
}
