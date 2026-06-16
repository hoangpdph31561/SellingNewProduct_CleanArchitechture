using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;

namespace SellingNewProduct.Domain.Queries;

/// <summary>
/// Input for <c>IPaymentQueries.SearchAsync</c>: all payment-list filters plus paging and
/// sorting. All filters are optional (null = no filter). The date range is on the created
/// date. <c>SortBy</c> accepts <c>date</c> (default, newest first) or <c>amount</c>.
/// </summary>
public sealed record PaymentSearchQuery
{
    public Guid? OrderId { get; init; }
    public PaymentMethod? Method { get; init; }
    public PaymentStatus? Status { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
