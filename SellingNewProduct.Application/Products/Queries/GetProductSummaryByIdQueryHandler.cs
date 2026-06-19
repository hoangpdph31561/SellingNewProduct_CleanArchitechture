using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Application.Products;

public sealed class GetProductSummaryByIdQueryHandler : IRequestHandler<GetProductSummaryByIdQuery, ProductSummaryView?>
{
    private readonly IProductReadService myProductReadService;

    public GetProductSummaryByIdQueryHandler(IProductReadService theProductReadService)
    {
        myProductReadService = theProductReadService;
    }

    public Task<ProductSummaryView?> Handle(GetProductSummaryByIdQuery theQuery, CancellationToken theCancellationToken)
        => myProductReadService.GetSummaryByIdAsync(theQuery.Id, theCancellationToken);
}
