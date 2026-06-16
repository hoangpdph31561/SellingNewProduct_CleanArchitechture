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
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService myPaymentService;
    private readonly IPaymentQueries myPaymentQueries;
    private readonly IValidator<CreatePaymentRequest> myCreateValidator;

    public PaymentsController(
        IPaymentService thePaymentService,
        IPaymentQueries thePaymentQueries,
        IValidator<CreatePaymentRequest> theCreateValidator)
    {
        myPaymentService = thePaymentService;
        myPaymentQueries = thePaymentQueries;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aPayment = await myPaymentService.GetByIdAsync(theId, theCancellationToken);
        return aPayment is null ? NotFound() : Ok(aPayment.ToResponse());
    }

    /// <summary>
    /// Search/filter payments (read side). Every filter is optional: by order id, method,
    /// status and a created-date range; sort by date/amount.
    /// <c>GET /api/payments/search?theStatus=Completed&amp;theSortBy=amount&amp;theSortDescending=true</c>
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<PaymentSummaryView>>> Search(
        [FromQuery] PaymentSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myPaymentQueries.SearchAsync(theQuery, theCancellationToken);
        return Ok(aResult);
    }

    /// <summary>
    /// Orders that still owe money (total minus completed payments), biggest balance first.
    /// <c>GET /api/payments/outstanding-orders?thePage=1&amp;thePageSize=20</c>
    /// </summary>
    [HttpGet("outstanding-orders")]
    public async Task<ActionResult<PagedResult<OutstandingOrderView>>> OutstandingOrders(
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await myPaymentQueries.GetOutstandingOrdersAsync(thePage, thePageSize, theCancellationToken);
        return Ok(aResult);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> Create(CreatePaymentRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aPayment = await myPaymentService.CreateAsync(theRequest.ToCommand(), theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aPayment.Id }, aPayment.ToResponse());
    }

    [HttpPost("{theId:guid}/complete")]
    public async Task<ActionResult<PaymentResponse>> Complete(Guid theId, CancellationToken theCancellationToken)
    {
        var aPayment = await myPaymentService.CompleteAsync(theId, theCancellationToken);
        return Ok(aPayment.ToResponse());
    }
}
