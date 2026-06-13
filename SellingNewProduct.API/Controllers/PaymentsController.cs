using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SellingNewProduct.API.Contracts;
using SellingNewProduct.API.Mapping;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentRepository myPaymentRepository;
    private readonly IOrderRepository myOrderRepository;
    private readonly IValidator<CreatePaymentRequest> myCreateValidator;

    public PaymentsController(
        IPaymentRepository thePaymentRepository,
        IOrderRepository theOrderRepository,
        IValidator<CreatePaymentRequest> theCreateValidator)
    {
        myPaymentRepository = thePaymentRepository;
        myOrderRepository = theOrderRepository;
        myCreateValidator = theCreateValidator;
    }

    [HttpGet("{theId:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetById(Guid theId, CancellationToken theCancellationToken)
    {
        var aPayment = await myPaymentRepository.GetByIdAsync(theId, theCancellationToken);
        return aPayment is null ? NotFound() : Ok(aPayment.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> Create(CreatePaymentRequest theRequest, CancellationToken theCancellationToken)
    {
        await myCreateValidator.ValidateAndThrowAsync(theRequest, theCancellationToken);

        var aOrder = await myOrderRepository.GetByIdAsync(theRequest.OrderId, theCancellationToken);
        if (aOrder is null)
        {
            return NotFound($"Order '{theRequest.OrderId}' not found.");
        }

        var aPayment = Payment.Create(
            theRequest.OrderId,
            Money.Create(theRequest.Amount, theRequest.Currency),
            (PaymentMethod)theRequest.Method);

        await myPaymentRepository.AddAsync(aPayment, theCancellationToken);

        return CreatedAtAction(nameof(GetById), new { theId = aPayment.Id }, aPayment.ToResponse());
    }

    [HttpPost("{theId:guid}/complete")]
    public async Task<ActionResult<PaymentResponse>> Complete(Guid theId, CancellationToken theCancellationToken)
    {
        var aPayment = await myPaymentRepository.GetByIdAsync(theId, theCancellationToken);
        if (aPayment is null)
        {
            return NotFound();
        }

        aPayment.MarkCompleted();
        await myPaymentRepository.UpdateAsync(aPayment, theCancellationToken);

        return Ok(aPayment.ToResponse());
    }
}
