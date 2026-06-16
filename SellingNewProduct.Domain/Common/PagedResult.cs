namespace SellingNewProduct.Domain.Common;

/// <summary>
/// One page of a larger result set, plus the metadata a UI needs to render paging
/// (current page, page size and the TOTAL number of matching rows across all pages).
///
/// Why a dedicated type instead of just returning a list? Because a list cannot tell
/// the caller "there are 4 380 matching orders, you are looking at rows 21-40". The
/// read side returns this so the list/report screens can show "Page 2 of 219" and
/// enable/disable the Next button — without ever loading every row into memory.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    /// <summary>Total number of pages (rounded up). 0 when there are no rows.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>True when there is a page before this one.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>True when there is a page after this one.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>An empty page (no rows), useful when a parent entity is not found.</summary>
    public static PagedResult<T> Empty(int thePage, int thePageSize) =>
        new(Array.Empty<T>(), thePage, thePageSize, 0);
}

/// <summary>
/// A normalised paging request. Clamps caller input to safe values in ONE place so
/// every query implementation (SQL and Mongo) shares the same rules: page is at
/// least 1, page size is between 1 and <see cref="MaxPageSize"/>. This stops a caller
/// from asking for page 0 or for 1 000 000 rows in a single request.
/// </summary>
public readonly record struct PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    public PageRequest(int thePage, int thePageSize)
    {
        Page = thePage < 1 ? 1 : thePage;
        PageSize = thePageSize < 1
            ? DefaultPageSize
            : thePageSize > MaxPageSize ? MaxPageSize : thePageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    /// <summary>How many rows to skip to reach this page (for <c>Skip(...)</c>).</summary>
    public int Skip => (Page - 1) * PageSize;
}
