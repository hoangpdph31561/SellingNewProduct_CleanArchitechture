using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Repositories.Read;

/// <summary>
/// MongoDB read side for customers. Customer documents live in MongoDB, but "top customers" is
/// ranked by ORDER spend — and orders live in SQL Server. So the ranking comes from
/// <see cref="ICrossDbOrderStats"/> (SQL) and the names are stitched in from MongoDB. Text filters
/// and sorting run in memory.
/// </summary>
internal sealed class MongoCustomerReadRepository : ICustomerReadRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoReadDbContext myMongoReadDbContext;
    private readonly ICrossDbOrderStats myOrderStats;

    public MongoCustomerReadRepository(MongoReadDbContext theMongoReadDbContext, ICrossDbOrderStats theOrderStats)
    {
        myMongoReadDbContext = theMongoReadDbContext;
        myOrderStats = theOrderStats;
    }

    public async Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoReadDbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : CustomerMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoReadDbContext.Customers
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(CustomerMapper.ToDomain).ToList();
    }

    public async Task<CustomerSummaryView?> GetSummaryByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aCustomer = await myMongoReadDbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == theCustomerId && c.Status != DeletedStatus, theCancellationToken);

        return aCustomer is null ? null : ToSummary(aCustomer);
    }

    public async Task<PagedResult<CustomerSummaryView>> SearchAsync(
        CustomerSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myMongoReadDbContext.Customers.AsNoTracking()
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

        // Ranking is order spend — that data lives in SQL, fetched via the cross-store stats port.
        var aRanked = (await myOrderStats.GetCustomerOrderTotalsAsync(theCancellationToken))
            .OrderByDescending(x => x.TotalSpent)
            .ToList();

        var aTotalCount = aRanked.Count;

        var aPageRows = aRanked
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToList();

        var aCustomerIds = aPageRows.Select(x => x.CustomerId).Distinct().ToList();
        var aNameById = (await myMongoReadDbContext.Customers.AsNoTracking()
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
