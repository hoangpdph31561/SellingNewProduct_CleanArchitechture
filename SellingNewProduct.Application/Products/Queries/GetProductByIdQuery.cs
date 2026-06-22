using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<Product?>;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly IProductReadService myProductReadService;

    public GetProductByIdQueryHandler(IProductReadService theProductReadService)
    {
        myProductReadService = theProductReadService;
    }

    public Task<Product?> Handle(GetProductByIdQuery theQuery, CancellationToken theCancellationToken)
        => myProductReadService.GetByIdAsync(theQuery.Id, theCancellationToken);
}
