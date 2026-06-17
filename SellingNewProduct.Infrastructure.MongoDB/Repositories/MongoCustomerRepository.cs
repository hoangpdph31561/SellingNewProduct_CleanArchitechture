using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Models;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories;

internal sealed class MongoCustomerRepository : ICustomerRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoCustomerRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : CustomerMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Customers
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(CustomerMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Customer theCustomer, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Customers.Add(CustomerMapper.ToDocument(theCustomer));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Customer theCustomer, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Customers.FirstOrDefaultAsync(r => r.Id == theCustomer.Id, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        CustomerMapper.MapInto(aDocument, theCustomer);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Customers.FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        var aDomain = CustomerMapper.ToDomain(aDocument);
        aDomain.Delete();
        CustomerMapper.MapInto(aDocument, aDomain);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    // --- Read side -------------------------------------------------------------------------
    // Simple field comparisons go to the database; the text "contains" filters and sorting
    // happen in memory (the documented Mongo tradeoff). "Top customers" has no JOIN, so orders
    // are loaded and grouped in memory, then customer names are stitched in for the page only.

    public async Task<CustomerSummaryView?> GetSummaryByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
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
