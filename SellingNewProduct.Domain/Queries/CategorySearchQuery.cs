using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Queries;

/// <summary>
/// Input for <c>ICategoryService.SearchAsync</c>: the optional name filter plus paging and
/// sort direction (results are always ordered by name). <c>Name</c> null = no filter.
/// </summary>
public sealed record CategorySearchQuery
{
    public string? Name { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    public bool SortDescending { get; init; }
}
