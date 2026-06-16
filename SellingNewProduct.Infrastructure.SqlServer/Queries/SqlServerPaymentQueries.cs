using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Queries;

/// <summary>
/// SQL Server read side for payments. The list/search runs on the database; the
/// "outstanding orders" report is a JOIN Orders x Customers plus a correlated SUM of
/// completed payments, with the unpaid filter pushed down. Soft-deleted rows are
/// excluded by the Global Query Filter.
/// </summary>
internal sealed class SqlServerPaymentQueries : IPaymentQueries
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerPaymentQueries(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<PagedResult<PaymentSummaryView>> SearchAsync(
        PaymentSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myAppDbContext.Payments.AsNoTracking();

        if (theQuery.OrderId is not null)
        {
            aQuery = aQuery.Where(p => p.OrderId == theQuery.OrderId);
        }

        if (theQuery.Method is not null)
        {
            var aMethodValue = (int)theQuery.Method.Value;
            aQuery = aQuery.Where(p => p.Method == aMethodValue);
        }

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(p => p.PaymentStatus == aStatusValue);
        }

        // Date range is on CreatedAtUtc so pending payments (no PaidAtUtc yet) are included.
        if (theQuery.FromUtc is not null)
        {
            aQuery = aQuery.Where(p => p.CreatedAtUtc >= theQuery.FromUtc);
        }

        if (theQuery.ToUtc is not null)
        {
            aQuery = aQuery.Where(p => p.CreatedAtUtc <= theQuery.ToUtc);
        }

        aQuery = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "amount" => theQuery.SortDescending ? aQuery.OrderByDescending(p => p.Amount) : aQuery.OrderBy(p => p.Amount),
            _ => theQuery.SortDescending ? aQuery.OrderBy(p => p.CreatedAtUtc) : aQuery.OrderByDescending(p => p.CreatedAtUtc)
        };

        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aRows = await aQuery
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .Select(p => new { p.Id, p.OrderId, p.Amount, p.Currency, p.Method, p.PaymentStatus, p.PaidAtUtc })
            .ToListAsync(theCancellationToken);

        var aItems = aRows
            .Select(r => new PaymentSummaryView(
                r.Id,
                r.OrderId,
                r.Amount,
                r.Currency,
                ((PaymentMethod)r.Method).ToString(),
                ((PaymentStatus)r.PaymentStatus).ToString(),
                r.PaidAtUtc))
            .ToList();

        return new PagedResult<PaymentSummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }

    public async Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        // For every real-sale order, compute the amount paid (SUM of completed payments)
        // as a correlated subquery, then keep only those not yet fully paid.
        var aQuery =
            from o in myAppDbContext.Orders.AsNoTracking()
            join c in myAppDbContext.Customers on o.CustomerId equals c.Id
            where o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped
            select new
            {
                o.Id,
                CustomerName = c.FullName,
                o.TotalAmount,
                o.TotalCurrency,
                AmountPaid = myAppDbContext.Payments
                    .Where(p => p.OrderId == o.Id && p.PaymentStatus == (int)PaymentStatus.Completed)
                    .Sum(p => (decimal?)p.Amount) ?? 0m
            };

        aQuery = aQuery.Where(x => x.AmountPaid < x.TotalAmount);

        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aRows = await aQuery
            .OrderByDescending(x => x.TotalAmount - x.AmountPaid)
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToListAsync(theCancellationToken);

        var aItems = aRows
            .Select(r => new OutstandingOrderView(
                r.Id,
                r.CustomerName,
                r.TotalAmount,
                r.AmountPaid,
                r.TotalAmount - r.AmountPaid,
                r.TotalCurrency))
            .ToList();

        return new PagedResult<OutstandingOrderView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
