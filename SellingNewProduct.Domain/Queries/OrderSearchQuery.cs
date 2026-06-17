using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;

namespace SellingNewProduct.Domain.Queries;

/// <summary>
/// Input for <c>IOrderService.SearchAsync</c>: all order-list filters plus paging and sorting
/// in one object. All filters are optional (null = no filter). <c>SortBy</c> accepts
/// <c>orderDate</c> (default, newest first), <c>totalAmount</c>, <c>customerName</c> or
/// <c>employeeName</c>.
/// </summary>
public sealed record OrderSearchQuery
{
    public Guid? CustomerId { get; init; }
    public Guid? EmployeeId { get; init; }
    public string? CustomerName { get; init; }
    public string? EmployeeName { get; init; }
    public OrderStatus? Status { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
