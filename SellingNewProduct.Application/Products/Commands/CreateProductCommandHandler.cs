using MediatR;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Product>
{
    private readonly IProductWriteService myProductWriteService;

    public CreateProductCommandHandler(IProductWriteService theProductWriteService)
    {
        myProductWriteService = theProductWriteService;
    }

    public Task<Product> Handle(CreateProductCommand theCommand, CancellationToken theCancellationToken) =>
        myProductWriteService.CreateAsync(theCommand.ToNewProduct(), theCancellationToken);
}
