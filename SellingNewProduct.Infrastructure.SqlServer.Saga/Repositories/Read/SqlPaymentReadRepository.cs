using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Repositories.Read;

/// <summary>
/// SQL read side for payments. The payment search runs entirely on SQL. The "outstanding orders"
/// report aggregates on SQL (order totals minus completed payments) and then enriches the page
/// with customer names fetched from MongoDB through <see cref="ICrossDbDirectory"/>.
/// </summary>
internal sealed class SqlPaymentReadRepository : IPaymentReadRepository
{
    private readonly AppDbContext myAppDbContext;
    private readonly ICrossDbDirectory myDirectory;

    public SqlPaymentReadRepository(AppDbContext theAppDbContext, ICrossDbDirectory theDirectory)
    {
        myAppDbContext = theAppDbContext;
        myDirectory = theDirectory;
    }

    public async Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : PaymentMapper.ToDomain(aRecord);
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

        var aQuery =
            from o in myAppDbContext.Orders.AsNoTracking()
            where o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped
            select new
            {
                o.Id,
                o.CustomerId,
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

        // Enrich just the page with customer names from MongoDB.
        var aCustomerNameById = await myDirectory.GetCustomerNamesAsync(theCancellationToken);

        var aItems = aRows
            .Select(r => new OutstandingOrderView(
                r.Id,
                aCustomerNameById.GetValueOrDefault(r.CustomerId, string.Empty),
                r.TotalAmount,
                r.AmountPaid,
                r.TotalAmount - r.AmountPaid,
                r.TotalCurrency))
            .ToList();

        return new PagedResult<OutstandingOrderView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
