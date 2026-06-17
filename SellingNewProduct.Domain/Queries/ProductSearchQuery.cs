using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Queries;

/// <summary>
/// Input for <c>IProductService.SearchAsync</c>: every catalogue filter plus paging and
/// sorting gathered into ONE object, so the port signature stays small and a new filter can
/// be added here without changing every caller. All filters are optional (null = no filter).
/// Public properties use <c>init</c> so ASP.NET Core can bind them from the query string
/// (<c>[FromQuery]</c>) while the defaults still apply when a value is omitted.
/// </summary>
public sealed record ProductSearchQuery
{
    public string? Name { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal? PriceFrom { get; init; }
    public decimal? PriceTo { get; init; }
    public int? MinStock { get; init; }
    public int? MaxStock { get; init; }
    public EntityStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
