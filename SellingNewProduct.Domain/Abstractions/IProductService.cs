using SellingNewProduct.Domain.Commands;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Products;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Abstractions;

/// <summary>
/// Product behavior — the single entry point the API depends on for both the write side
/// (aggregate operations) and the read side (enriched catalogue projections).
/// </summary>
public interface IProductService
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<Product?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default);

    Task<Product> CreateAsync(CreateProductCommand theCommand, CancellationToken theCancellationToken = default);

    /// <summary>Creates several products in one go (bulk import) — enforces the unique-SKU rule across the batch and the store.</summary>
    Task<IReadOnlyList<Product>> CreateManyAsync(IReadOnlyList<CreateProductCommand> theCommands, CancellationToken theCancellationToken = default);

    /// <summary>One enriched product row (with the category name), or <c>null</c> — the flat read-side counterpart of <see cref="GetByIdAsync"/>.</summary>
    Task<ProductSummaryView?> GetSummaryByIdAsync(Guid theProductId, CancellationToken theCancellationToken = default);

    /// <summary>Search/filter the catalogue (see <see cref="ProductSearchQuery"/>), one page of enriched rows plus the total count.</summary>
    Task<PagedResult<ProductSummaryView>> SearchAsync(ProductSearchQuery theQuery, CancellationToken theCancellationToken = default);
}
