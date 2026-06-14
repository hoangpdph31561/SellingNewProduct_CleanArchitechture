using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.Application.Common;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;

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
}
