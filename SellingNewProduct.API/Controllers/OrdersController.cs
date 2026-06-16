using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService myOrderService;
    private readonly IOrderQueries myOrderQueries;
    private readonly IValidator<CreateOrderRequest> myCreateValidator;
    private readonly IValidator<AddOrderDetailRequest> myAddDetailValidator;

    public OrdersController(
        IOrderService theOrderService,
        IOrderQueries theOrderQueries,
        IValidator<CreateOrderRequest> theCreateValidator,
        IValidator<AddOrderDetailRequest> theAddDetailValidator)
    {
        myOrderService = theOrderService;
        myOrderQueries = theOrderQueries;
        myCreateValidator = theCreateValidator;
        myAddDetailValidator = theAddDetailValidator;
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderService.GetByIdAsync(theId, theCancellationToken);
        return aOrder is null ? NotFound() : Ok(aOrder.ToResponse());
    }

    /// <summary>
    /// Order detail enriched for display: includes the customer NAME and selling employee
    /// NAME (instead of just CustomerId/EmployeeId). Uses the read side (JOIN) rather than
    /// loading the aggregate.
    /// </summary>
    [HttpGet("{theId:guid}/view")]
    public async Task<ActionResult<OrderDetailView>> GetView(Guid theId, CancellationToken theCancellationToken)
    {
        var aView = await myOrderQueries.GetOrderDetailAsync(theId, theCancellationToken);
        return aView is null ? NotFound() : Ok(aView);
    }

    /// <summary>
    /// List/search orders (with customer + employee names), paginated. Every filter is optional;
    /// you can filter by customer/employee id OR by a "contains" on their name, by status and by
    /// an order-date range, and sort by orderDate/totalAmount/customerName/employeeName.
    /// Example: <c>GET /api/orders?theCustomerName=an&amp;theStatus=Confirmed&amp;theSortBy=totalAmount&amp;theSortDescending=true&amp;thePage=2&amp;thePageSize=20</c>
    /// Returns one page plus the total count (page/pageSize are clamped to safe values).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderSummaryView>>> Search(
        [FromQuery] OrderSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myOrderQueries.SearchAsync(theQuery, theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>
    /// How many orders sit in each status and the total amount they represent — a
    /// dashboard breakdown (GROUP BY status). <c>GET /api/orders/status-breakdown</c>
    /// </summary>
    [HttpGet("status-breakdown")]
    public async Task<ActionResult<IReadOnlyList<OrderStatusCountView>>> StatusBreakdown(CancellationToken theCancellationToken)
    {
        var aResult = await myOrderQueries.GetStatusBreakdownAsync(theCancellationToken);
        return Ok(aResult);
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aOrder = await myOrderService.CreateAsync(theRequest.ToCommand(), theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aOrder.Id }, aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/details")]
    public async Task<ActionResult<OrderResponse>> AddDetail(Guid theId, AddOrderDetailRequest theRequest, CancellationToken theCancellationToken)
    {
        await myAddDetailValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aOrder = await myOrderService.AddDetailAsync(theId, theRequest.ProductId, theRequest.Quantity, theCancellationToken);
        return Ok(aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/confirm")]
    public async Task<ActionResult<OrderResponse>> Confirm(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderService.ConfirmAsync(theId, theCancellationToken);
        return Ok(aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/ship")]
    public async Task<ActionResult<OrderResponse>> Ship(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderService.ShipAsync(theId, theCancellationToken);
        return Ok(aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderService.CancelAsync(theId, theCancellationToken);
        return Ok(aOrder.ToResponse());
    }
}
