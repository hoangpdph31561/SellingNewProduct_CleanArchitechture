using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Queries;

/// <summary>
/// MongoDB read side for payments. The payment list is plain field comparisons, so it
/// runs on the database. "Outstanding orders" needs the amount paid per order — a join in
/// SQL — which here is computed by loading the completed payments and grouping them in
/// memory. Soft-deleted rows are excluded explicitly.
/// </summary>
internal sealed class MongoPaymentQueries : IPaymentQueries
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;
    private const int CompletedPayment = (int)PaymentStatus.Completed;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoPaymentQueries(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<PagedResult<PaymentSummaryView>> SearchAsync(
        PaymentSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myMongoAppDbContext.Payments.AsNoTracking()
            .Where(p => p.Status != DeletedStatus);

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
            .ToListAsync(theCancellationToken);

        var aItems = aRows
            .Select(p => new PaymentSummaryView(
                p.Id,
                p.OrderId,
                p.Amount,
                p.Currency,
                ((PaymentMethod)p.Method).ToString(),
                ((PaymentStatus)p.PaymentStatus).ToString(),
                p.PaidAtUtc))
            .ToList();

        return new PagedResult<PaymentSummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }

    public async Task<PagedResult<OutstandingOrderView>> GetOutstandingOrdersAsync(
        int thePage = 1,
        int thePageSize = PageRequest.DefaultPageSize,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        var aOrders = await myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus &&
                        (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped))
            .ToListAsync(theCancellationToken);

        // Amount paid per order = SUM of completed payments, grouped in memory (no JOIN).
        var aPayments = await myMongoAppDbContext.Payments.AsNoTracking()
            .Where(p => p.Status != DeletedStatus && p.PaymentStatus == CompletedPayment)
            .ToListAsync(theCancellationToken);

        var aPaidByOrder = aPayments
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var aOutstanding = aOrders
            .Select(o =>
            {
                var aPaid = aPaidByOrder.TryGetValue(o.Id, out var aValue) ? aValue : 0m;
                return new
                {
                    o.Id,
                    o.CustomerId,
                    o.TotalAmount,
                    o.TotalCurrency,
                    AmountPaid = aPaid,
                    Balance = o.TotalAmount - aPaid
                };
            })
            .Where(x => x.Balance > 0m)
            .OrderByDescending(x => x.Balance)
            .ToList();

        var aTotalCount = aOutstanding.Count;

        var aPageRows = aOutstanding
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToList();

        // Stitch customer names for this page only.
        var aCustomerIds = aPageRows.Select(x => x.CustomerId).Distinct().ToList();
        var aNameById = (await myMongoAppDbContext.Customers.AsNoTracking()
            .Where(c => aCustomerIds.Contains(c.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(c => c.Id, c => c.FullName);

        var aItems = aPageRows
            .Select(x => new OutstandingOrderView(
                x.Id,
                aNameById.TryGetValue(x.CustomerId, out var aName) ? aName : "(unknown)",
                x.TotalAmount,
                x.AmountPaid,
                x.Balance,
                x.TotalCurrency))
            .ToList();

        return new PagedResult<OutstandingOrderView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
