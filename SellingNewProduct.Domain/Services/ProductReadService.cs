using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Interfaces.Inbound;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Services;

/// <summary>Implements the product read port; forwards to the read repository (no business rules).</summary>
public sealed class ProductReadService : IProductReadService
{
    private readonly IProductReadRepository myProductRepository;

    public ProductReadService(IProductReadRepository theProductRepository)
    {
        myProductRepository = theProductRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default)
        => myProductRepository.GetAllAsync(theCancellationToken);

    public Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myProductRepository.GetByIdAsync(theId, theCancellationToken);

    public Task<ProductSummaryView?> GetSummaryByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
        => myProductRepository.GetSummaryByIdAsync(theId, theCancellationToken);

    public Task<PagedResult<ProductSummaryView>> SearchAsync(ProductSearchQuery theCriteria, CancellationToken theCancellationToken = default)
        => myProductRepository.SearchAsync(theCriteria, theCancellationToken);
}
