using SellingNewProduct.Domain.Categories;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;

namespace SellingNewProduct.Domain.Interfaces.Inbound;

/// <summary>Inbound (driving) port for category reads.</summary>
public interface ICategoryReadService : IReadService<Category>
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<CategorySummaryView>> GetSummariesAsync(CancellationToken theCancellationToken = default);

    Task<PagedResult<CategorySummaryView>> SearchAsync(CategorySearchQuery theCriteria, CancellationToken theCancellationToken = default);
}
