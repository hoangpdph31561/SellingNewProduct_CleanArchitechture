using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

/// <summary>
/// Reporting endpoints — all backed by the read side (<see cref="IReportQueries"/>):
/// JOIN + GROUP BY across several tables to produce aggregate numbers, bypassing the
/// domain aggregate.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportQueries myReportQueries;

    public ReportsController(IReportQueries theReportQueries)
    {
        myReportQueries = theReportQueries;
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
        var aResult = await myReportQueries.GetBestSellingProductsAsync(thePage, thePageSize, theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>Sales leaderboard per selling employee.</summary>
    [HttpGet("employee-sales")]
    public async Task<ActionResult<IReadOnlyList<EmployeeSalesView>>> EmployeeSales(CancellationToken theCancellationToken)
    {
        var aResult = await myReportQueries.GetEmployeeSalesLeaderboardAsync(theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>Revenue grouped by product category, best-selling category first.</summary>
    [HttpGet("sales-by-category")]
    public async Task<ActionResult<IReadOnlyList<CategorySalesView>>> SalesByCategory(CancellationToken theCancellationToken)
    {
        var aResult = await myReportQueries.GetSalesByCategoryAsync(theCancellationToken);
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
        var aResult = await myReportQueries.GetDailySalesAsync(theFromUtc, theToUtc, theCancellationToken);
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
        var aResult = await myReportQueries.GetLowStockProductsAsync(theThreshold, thePage, thePageSize, theCancellationToken);
        return Ok(aResult);
    }
}
