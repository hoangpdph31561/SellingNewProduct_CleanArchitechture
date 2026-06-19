using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

public sealed class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, IReadOnlyList<Product>>
{
    private readonly IProductReadService myProductReadService;

    public GetAllProductsQueryHandler(IProductReadService theProductReadService)
    {
        myProductReadService = theProductReadService;
    }

    public Task<IReadOnlyList<Product>> Handle(GetAllProductsQuery theQuery, CancellationToken theCancellationToken)
        => myProductReadService.GetAllAsync(theCancellationToken);
}
