using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Queries;

/// <summary>
/// Input for <c>IEmployeeService.SearchAsync</c>: all employee-list filters plus paging and
/// sorting. All filters are optional (null = no filter). <c>SortBy</c> accepts <c>name</c>
/// (default), <c>position</c> or <c>hiredate</c>.
/// </summary>
public sealed record EmployeeSearchQuery
{
    public string? Name { get; init; }
    public string? Position { get; init; }
    public EntityStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
