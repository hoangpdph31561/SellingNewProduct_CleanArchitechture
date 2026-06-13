using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderRepository myOrderRepository;
    private readonly ICustomerRepository myCustomerRepository;
    private readonly IEmployeeRepository myEmployeeRepository;
    private readonly IProductRepository myProductRepository;
    private readonly IOrderQueries myOrderQueries;
    private readonly IValidator<CreateOrderRequest> myCreateValidator;
    private readonly IValidator<AddOrderDetailRequest> myAddDetailValidator;

    public OrdersController(
        IOrderRepository theOrderRepository,
        ICustomerRepository theCustomerRepository,
        IEmployeeRepository theEmployeeRepository,
        IProductRepository theProductRepository,
        IOrderQueries theOrderQueries,
        IValidator<CreateOrderRequest> theCreateValidator,
        IValidator<AddOrderDetailRequest> theAddDetailValidator)
    {
        myOrderRepository = theOrderRepository;
        myCustomerRepository = theCustomerRepository;
        myEmployeeRepository = theEmployeeRepository;
        myProductRepository = theProductRepository;
        myOrderQueries = theOrderQueries;
        myCreateValidator = theCreateValidator;
        myAddDetailValidator = theAddDetailValidator;
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderRepository.GetByIdAsync(theId, theCancellationToken);
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
    /// List/search orders (with customer + employee names). Every parameter is optional.
    /// Example: <c>GET /api/orders?theStatus=Confirmed&amp;theEmployeeId=...</c>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryView>>> Search(
        [FromQuery] Guid? theCustomerId,
        [FromQuery] Guid? theEmployeeId,
        [FromQuery] OrderStatus? theStatus,
        [FromQuery] DateTime? theFromUtc,
        [FromQuery] DateTime? theToUtc,
        CancellationToken theCancellationToken)
    {
        var aResult = await myOrderQueries.SearchAsync(
            theCustomerId, theEmployeeId, theStatus, theFromUtc, theToUtc, theCancellationToken);
        return Ok(aResult);
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aCustomer = await myCustomerRepository.GetByIdAsync(theRequest.CustomerId, theCancellationToken);
        if (aCustomer is null)
        {
            return NotFound($"Customer '{theRequest.CustomerId}' not found.");
        }

        var aEmployee = await myEmployeeRepository.GetByIdAsync(theRequest.EmployeeId, theCancellationToken);
        if (aEmployee is null)
        {
            return NotFound($"Employee '{theRequest.EmployeeId}' not found.");
        }

        var aShippingAddress = Address.Create(
            theRequest.ShippingAddress.Street,
            theRequest.ShippingAddress.Ward,
            theRequest.ShippingAddress.District,
            theRequest.ShippingAddress.City,
            theRequest.ShippingAddress.Country);

        var aOrder = Order.Create(theRequest.CustomerId, theRequest.EmployeeId, aShippingAddress);
        await myOrderRepository.AddAsync(aOrder, theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aOrder.Id }, aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/details")]
    public async Task<ActionResult<OrderResponse>> AddDetail(Guid theId, AddOrderDetailRequest theRequest, CancellationToken theCancellationToken)
    {
        await myAddDetailValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aOrder = await myOrderRepository.GetByIdAsync(theId, theCancellationToken);
        if (aOrder is null)
        {
            return NotFound($"Order '{theId}' not found.");
        }

        var aProduct = await myProductRepository.GetByIdAsync(theRequest.ProductId, theCancellationToken);
        if (aProduct is null)
        {
            return NotFound($"Product '{theRequest.ProductId}' not found.");
        }

        aOrder.AddDetail(aProduct, theRequest.Quantity);
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);

        return Ok(aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/confirm")]
    public async Task<ActionResult<OrderResponse>> Confirm(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderRepository.GetByIdAsync(theId, theCancellationToken);
        if (aOrder is null)
        {
            return NotFound();
        }

        aOrder.Confirm();
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);

        return Ok(aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/ship")]
    public async Task<ActionResult<OrderResponse>> Ship(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderRepository.GetByIdAsync(theId, theCancellationToken);
        if (aOrder is null)
        {
            return NotFound();
        }

        aOrder.MarkShipped();
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);

        return Ok(aOrder.ToResponse());
    }

    [HttpPost("{theId:guid}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid theId, CancellationToken theCancellationToken)
    {
        var aOrder = await myOrderRepository.GetByIdAsync(theId, theCancellationToken);
        if (aOrder is null)
        {
            return NotFound();
        }

        aOrder.Cancel();
        await myOrderRepository.UpdateAsync(aOrder, theCancellationToken);

        return Ok(aOrder.ToResponse());
    }
}
