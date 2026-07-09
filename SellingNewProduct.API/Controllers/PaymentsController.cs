using MediatR;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Application.Payments;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentsController : ControllerBase
{
    private readonly ISender mySender;
    private readonly IPaymentGateway myPaymentGateway;

    public PaymentsController(ISender theSender, IPaymentGateway thePaymentGateway)
    {
        mySender = theSender;
        myPaymentGateway = thePaymentGateway;
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aPayment = await mySender.Send(new GetPaymentByIdQuery(theId), theCancellationToken);
        return aPayment is null ? NotFound() : Ok(aPayment.ToResponse());
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<PaymentSummaryView>>> Search(
        [FromQuery] PaymentSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new SearchPaymentsQuery(theQuery), theCancellationToken);
        return Ok(aResult);
    }

    [HttpGet("outstanding-orders")]
    public async Task<ActionResult<PagedResult<OutstandingOrderView>>> OutstandingOrders(
        [FromQuery] int thePage = 1,
        [FromQuery] int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default)
    {
        var aResult = await mySender.Send(new GetOutstandingOrdersQuery(thePage, thePageSize), theCancellationToken);
        return Ok(aResult);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> Create(CreatePaymentRequest theRequest, CancellationToken theCancellationToken)
    {
        var aPayment = await mySender.Send(theRequest.ToCommand(), theCancellationToken);
        return CreatedAtAction(nameof(GetById), new { theId = aPayment.Id }, aPayment.ToResponse());
    }

    [HttpPost("{theId:guid}/complete")]
    public async Task<ActionResult<PaymentResponse>> Complete(Guid theId, CancellationToken theCancellationToken)
    {
        var aPayment = await mySender.Send(new CompletePaymentCommand(theId), theCancellationToken);
        return Ok(aPayment.ToResponse());
    }

    /// <summary>Starts a VNPay payment: returns the signed redirect URL to send the customer to.</summary>
    [HttpPost("vnpay/create")]
    public ActionResult<VnPayPaymentUrlResponse> CreateVnPayPayment(CreateVnPayPaymentRequest theRequest)
    {
        var aClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var aResult = myPaymentGateway.CreatePayment(new PaymentGatewayRequest(
            theRequest.OrderId, theRequest.Amount, "VND", theRequest.OrderInfo, aClientIp));

        return Ok(new VnPayPaymentUrlResponse(aResult.PaymentUrl));
    }

    /// <summary>
    /// VNPay redirects the customer here after payment. The signature is verified FIRST (proves the
    /// call really came from VNPay and was untampered); only a valid+successful result should mark the
    /// order paid. Wire the domain completion (e.g. look up the payment by order and send
    /// <see cref="CompletePaymentCommand"/>) where indicated.
    /// </summary>
    [HttpGet("vnpay-return")]
    public async Task<ActionResult<VnPayReturnResponse>> VnPayReturn(CancellationToken theCancellationToken)
    {
        var aParameters = Request.Query.ToDictionary(p => p.Key, p => p.Value.ToString());
        var aResult = myPaymentGateway.VerifyCallback(aParameters);

        // Signature invalid → do NOT trust the outcome, do NOT touch the order.
        if (!aResult.IsValid)
        {
            return BadRequest(Map(aResult, thePaymentCompleted: false));
        }

        // Verified + paid → complete the order's pending payment. Idempotent: VNPay may call the return
        // URL and the IPN (possibly more than once); a duplicate finds the payment already completed.
        var aPaymentCompleted = false;
        if (aResult.IsSuccessful)
        {
            var aPayment = await mySender.Send(new CompletePaymentByOrderCommand(aResult.OrderId), theCancellationToken);
            aPaymentCompleted = aPayment is not null;
        }

        return Ok(Map(aResult, aPaymentCompleted));
    }

    private static VnPayReturnResponse Map(PaymentCallbackResult theResult, bool thePaymentCompleted) =>
        new(theResult.IsValid, theResult.IsSuccessful, theResult.OrderId, theResult.Amount,
            theResult.TransactionReference, theResult.ResponseCode, thePaymentCompleted);
}
