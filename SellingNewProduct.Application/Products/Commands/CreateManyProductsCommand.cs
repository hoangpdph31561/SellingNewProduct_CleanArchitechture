using MediatR;
using SellingNewProduct.Domain.Products;

namespace SellingNewProduct.Application.Products;

/// <summary>Write-side command: bulk create. The unique-SKU rule is enforced within the batch and the store.</summary>
public sealed record CreateManyProductsCommand(IReadOnlyList<CreateProductCommand> Items)
    : IRequest<IReadOnlyList<Product>>;
