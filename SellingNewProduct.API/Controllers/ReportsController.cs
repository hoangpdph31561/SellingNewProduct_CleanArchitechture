using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.Application.Reports;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

/// <summary>
/// Reporting endpoints — all backed by report queries (JOIN + GROUP BY across several tables to
/// produce aggregate numbers, bypassing the domain aggregate). Sent through MediatR.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public sealed class ReportsController : ControllerBase
{
    private readonly ISender mySender;

    public ReportsController(ISender theSender)
    {
        mySender = theSender;
    }

    [HttpGet("best-selling-products")]
    public async Task<ActionResult<PagedResult<BestSellingProductView>>> BestSellingProducts(
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new GetBestSellingProductsQuery(thePage, thePageSize), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("employee-sales")]
    public async Task<ActionResult<IReadOnlyList<EmployeeSalesView>>> EmployeeSales(CancellationToken theCancellationToken)
    {
        var aResult = await mySender.Send(new GetEmployeeSalesLeaderboardQuery(), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("sales-by-category")]
    public async Task<ActionResult<IReadOnlyList<CategorySalesView>>> SalesByCategory(CancellationToken theCancellationToken)
    {
        var aResult = await mySender.Send(new GetSalesByCategoryQuery(), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("daily-sales")]
    public async Task<ActionResult<IReadOnlyList<DailySalesView>>> DailySales(
        [FromQuery] DateTime? theFromUtc,
        [FromQuery] DateTime? theToUtc,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new GetDailySalesQuery(theFromUtc, theToUtc), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("low-stock-products")]
    public async Task<ActionResult<PagedResult<LowStockProductView>>> LowStockProducts(
        [FromQuery] int theThreshold = 5,
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new GetLowStockProductsQuery(theThreshold, thePage, thePageSize), theCancellationToken);
        return Ok(aResult);
    }
}
