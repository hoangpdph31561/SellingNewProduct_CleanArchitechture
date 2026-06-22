using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Application.Payments;

public sealed record GetPaymentByIdQuery(Guid Id) : IRequest<Payment?>;

public sealed class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, Payment?>
{
    private readonly IPaymentReadService myPaymentReadService;

    public GetPaymentByIdQueryHandler(IPaymentReadService thePaymentReadService)
    {
        myPaymentReadService = thePaymentReadService;
    }

    public Task<Payment?> Handle(GetPaymentByIdQuery theQuery, CancellationToken theCancellationToken)
        => myPaymentReadService.GetByIdAsync(theQuery.Id, theCancellationToken);
}
