using MediatR;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Orders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender mySender;

    public OrdersController(ISender theSender)
    {
        mySender = theSender;
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await mySender.Send(new GetOrderByIdQuery(theId), theCancellationToken);
        return aOrder is null ? NotFound() : Ok(aOrder.ToResponse());
    }

    [HttpGet("{theId:guid}/view")]
    public async Task<ActionResult<OrderDetailView>> GetView(Guid theId, CancellationToken theCancellationToken)
    {
        var aView = await mySender.Send(new GetOrderDetailViewQuery(theId), theCancellationToken);
        return aView is null ? NotFound() : Ok(aView);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderSummaryView>>> Search(
        [FromQuery] OrderSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new SearchOrdersQuery(theQuery), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("status-breakdown")]
    public async Task<ActionResult<IReadOnlyList<OrderStatusCountView>>> StatusBreakdown(CancellationToken theCancellationToken)
    {
        var aResult = await mySender.Send(new GetOrderStatusBreakdownQuery(), theCancellationToken);
        return Ok(aResult);
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Place(PlaceOrderRequest theRequest, CancellationToken theCancellationToken)
    {
        var aOrder = await mySender.Send(theRequest.ToCommand(), theCancellationToken);
        return CreatedAtAction(nameof(GetById), new { theId = aOrder.Id }, aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/confirm")]
    public async Task<ActionResult<OrderResponse>> Confirm(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await mySender.Send(new ConfirmOrderCommand(theId), theCancellationToken);
        return Ok(aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/ship")]
    public async Task<ActionResult<OrderResponse>> Ship(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await mySender.Send(new ShipOrderCommand(theId), theCancellationToken);
        return Ok(aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await mySender.Send(new CancelOrderCommand(theId), theCancellationToken);
        return Ok(aOrder.ToResponse());
    }
}
