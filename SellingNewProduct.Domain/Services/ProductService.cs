using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Domain.ValueObjects;

namespace SellingNewProduct.Domain.Services;

internal sealed class ProductService : IProductService
{
    private readonly IProductRepository myProductRepository;
    private readonly ICategoryRepository myCategoryRepository;

    public ProductService(IProductRepository theProductRepository, ICategoryRepository theCategoryRepository)
    {
        myProductRepository = theProductRepository;
        myCategoryRepository = theCategoryRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myProductRepository.GetAllAsync(theCancellationToken);

    public Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myProductRepository.GetByIdAsync(theId, theCancellationToken);

    public async Task<Product> CreateAsync(CreateProductCommand theCommand, CancellationToken theCancellationToken = default)
    {
        // A product must reference an existing category.
        var aCategory = await myCategoryRepository.GetByIdAsync(theCommand.CategoryId, theCancellationToken);
        if (aCategory is null)
        {
            throw new NotFoundException($"Category '{theCommand.CategoryId}' not found.");
        }

        var aProduct = Product.Create(
            theCommand.Name,
            Sku.Create(theCommand.Sku),
            theCommand.Color,
            (Size)theCommand.Size,
            Money.Create(theCommand.Price, theCommand.Currency),
            theCommand.StockQuantity,
            theCommand.CategoryId);

        await myProductRepository.AddAsync(aProduct, theCancellationToken);
        return aProduct;
    }
}
