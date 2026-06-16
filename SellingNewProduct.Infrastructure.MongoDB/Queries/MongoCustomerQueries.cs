using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Infrastructure.MongoDB.Models;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Queries;

/// <summary>
/// MongoDB read side for customers. Simple field comparisons go to the database; the
/// text "contains" filters and sorting happen in memory (the documented Mongo tradeoff).
/// "Top customers" has no JOIN, so orders are loaded and grouped in memory, then the
/// customer names are stitched in for the requested page only. Mongo has no Global Query
/// Filter, so each query excludes soft-deleted rows itself.
/// </summary>
internal sealed class MongoCustomerQueries : ICustomerQueries
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoCustomerQueries(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<CustomerSummaryView?> GetByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aCustomer = await myMongoAppDbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == theCustomerId && c.Status != DeletedStatus, theCancellationToken);

        return aCustomer is null ? null : ToSummary(aCustomer);
    }

    public async Task<PagedResult<CustomerSummaryView>> SearchAsync(
        CustomerSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myMongoAppDbContext.Customers.AsNoTracking()
            .Where(c => c.Status != DeletedStatus);

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(c => c.Status == aStatusValue);
        }

        var aCandidates = await aQuery.ToListAsync(theCancellationToken);

        IEnumerable<CustomerDocument> aFiltered = aCandidates;

        if (!string.IsNullOrWhiteSpace(theQuery.Name))
        {
            var aName = theQuery.Name.Trim();
            aFiltered = aFiltered.Where(c => c.FullName.Contains(aName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.Email))
        {
            var aEmail = theQuery.Email.Trim();
            aFiltered = aFiltered.Where(c => c.Email.Contains(aEmail, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.PhoneNumber))
        {
            var aPhone = theQuery.PhoneNumber.Trim();
            aFiltered = aFiltered.Where(c => c.PhoneNumber.Contains(aPhone, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.City))
        {
            var aCity = theQuery.City.Trim();
            aFiltered = aFiltered.Where(c => c.City.Contains(aCity, StringComparison.OrdinalIgnoreCase));
        }

        aFiltered = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "email" => theQuery.SortDescending ? aFiltered.OrderByDescending(c => c.Email) : aFiltered.OrderBy(c => c.Email),
            "city" => theQuery.SortDescending ? aFiltered.OrderByDescending(c => c.City) : aFiltered.OrderBy(c => c.City),
            _ => theQuery.SortDescending ? aFiltered.OrderByDescending(c => c.FullName) : aFiltered.OrderBy(c => c.FullName)
        };

        var aMatched = aFiltered.ToList();

        var aItems = aMatched
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .Select(ToSummary)
            .ToList();

        return new PagedResult<CustomerSummaryView>(aItems, aPage.Page, aPage.PageSize, aMatched.Count);
    }

    public async Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        // No JOIN: load real-sale orders, group by customer in memory.
        var aOrders = await myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus &&
                        (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped))
            .ToListAsync(theCancellationToken);

        var aRanked = aOrders
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                TotalOrders = g.Count(),
                TotalSpent = g.Sum(o => o.TotalAmount),
                Currency = g.Select(o => o.TotalCurrency).FirstOrDefault() ?? "VND"
            })
            .OrderByDescending(x => x.TotalSpent)
            .ToList();

        var aTotalCount = aRanked.Count;

        var aPageRows = aRanked
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToList();

        // Stitch the customer names for this page's rows only.
        var aCustomerIds = aPageRows.Select(x => x.CustomerId).Distinct().ToList();
        var aNameById = (await myMongoAppDbContext.Customers.AsNoTracking()
            .Where(c => aCustomerIds.Contains(c.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(c => c.Id, c => c.FullName);

        var aItems = aPageRows
            .Select(x => new TopCustomerView(
                x.CustomerId,
                aNameById.TryGetValue(x.CustomerId, out var aName) ? aName : "(unknown)",
                x.TotalOrders,
                x.TotalSpent,
                x.Currency))
            .ToList();

        return new PagedResult<TopCustomerView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }

    private static CustomerSummaryView ToSummary(CustomerDocument theDoc) =>
        new(theDoc.Id, theDoc.FullName, theDoc.Email, theDoc.PhoneNumber, theDoc.City, ((EntityStatus)theDoc.Status).ToString());
}
