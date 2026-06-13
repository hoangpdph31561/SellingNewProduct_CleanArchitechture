using Microsoft.AspNetCore.Mvc;
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

    /// <summary>Top N best-selling products (with category name). E.g. <c>GET /api/reports/best-selling-products?theTop=5</c></summary>
    [HttpGet("best-selling-products")]
    public async Task<ActionResult<IReadOnlyList<BestSellingProductView>>> BestSellingProducts(
        [FromQuery] int theTop = 10,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myReportQueries.GetBestSellingProductsAsync(theTop, theCancellationToken);
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
