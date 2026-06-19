using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

public sealed class CreateManyProductsCommandHandler : IRequestHandler<CreateManyProductsCommand, IReadOnlyList<Product>>
{
    private readonly IProductWriteService myProductWriteService;

    public CreateManyProductsCommandHandler(IProductWriteService theProductWriteService)
    {
        myProductWriteService = theProductWriteService;
    }

    public Task<IReadOnlyList<Product>> Handle(CreateManyProductsCommand theCommand, CancellationToken theCancellationToken)
    {
        var aRequests = theCommand.Items.Select(i => i.ToNewProduct()).ToList();
        return myProductWriteService.CreateManyAsync(aRequests, theCancellationToken);
    }
}
