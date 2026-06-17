using SellingNewProduct.Domain.Common;

namespace SellingNewProduct.Domain.Queries;

/// <summary>
/// Input for <c>ICustomerService.SearchAsync</c>: all customer-list filters plus paging and
/// sorting. All filters are optional (null = no filter). <c>SortBy</c> accepts <c>name</c>
/// (default), <c>email</c> or <c>city</c>.
/// </summary>
public sealed record CustomerSearchQuery
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? City { get; init; }
    public EntityStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
