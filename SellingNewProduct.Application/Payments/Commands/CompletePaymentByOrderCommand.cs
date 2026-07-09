using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Application.Payments;

/// <summary>
/// Completes an order's pending payment from a gateway callback (VNPay return/IPN), which only knows the
/// order id. Idempotent — the domain service treats a duplicate call as a no-op. Result is null when the
/// order had no payment to complete.
/// </summary>
public sealed record CompletePaymentByOrderCommand(Guid OrderId) : IRequest<Payment?>;

public sealed class CompletePaymentByOrderCommandHandler : IRequestHandler<CompletePaymentByOrderCommand, Payment?>
{
    private readonly IPaymentWriteService myPaymentWriteService;

    public CompletePaymentByOrderCommandHandler(IPaymentWriteService thePaymentWriteService)
    {
        myPaymentWriteService = thePaymentWriteService;
    }

    public Task<Payment?> Handle(CompletePaymentByOrderCommand theCommand, CancellationToken theCancellationToken) =>
        myPaymentWriteService.CompleteByOrderAsync(theCommand.OrderId, theCancellationToken);
}
