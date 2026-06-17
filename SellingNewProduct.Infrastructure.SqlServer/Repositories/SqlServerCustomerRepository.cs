using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories;

internal sealed class SqlServerCustomerRepository : ICustomerRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerCustomerRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : CustomerMapper.ToDomain(aRecord);
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Customers.AsNoTracking().ToListAsync(theCancellationToken);
        return aRecords.Select(CustomerMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Customer theCustomer, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Customers.Add(CustomerMapper.ToRecord(theCustomer));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Customer theCustomer, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Customers.FirstOrDefaultAsync(r => r.Id == theCustomer.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        CustomerMapper.MapInto(aRecord, theCustomer);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Customers.FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);
        if (aRecord == null)
        {
            return;
        }
        var aDomain = CustomerMapper.ToDomain(aRecord);
        aDomain.Delete();
        CustomerMapper.MapInto(aRecord, aDomain);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    // --- Read side -------------------------------------------------------------------------
    // The plain list/search runs entirely on the database (WHERE / ORDER BY / OFFSET-FETCH);
    // the "top customers" report is a JOIN Orders x Customers + GROUP BY pushed down to the
    // database. Soft-deleted rows are excluded by the Global Query Filter.

    public async Task<CustomerSummaryView?> GetSummaryByIdAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aRow = await myAppDbContext.Customers.AsNoTracking()
            .Where(c => c.Id == theCustomerId)
            .Select(c => new { c.Id, c.FullName, c.Email, c.PhoneNumber, c.City, c.Status })
            .FirstOrDefaultAsync(theCancellationToken);

        return aRow is null
            ? null
            : new CustomerSummaryView(aRow.Id, aRow.FullName, aRow.Email, aRow.PhoneNumber, aRow.City, ((EntityStatus)aRow.Status).ToString());
    }

    public async Task<PagedResult<CustomerSummaryView>> SearchAsync(
        CustomerSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myAppDbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(theQuery.Name))
        {
            var aName = theQuery.Name.Trim();
            aQuery = aQuery.Where(c => c.FullName.Contains(aName));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.Email))
        {
            var aEmail = theQuery.Email.Trim();
            aQuery = aQuery.Where(c => c.Email.Contains(aEmail));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.PhoneNumber))
        {
            var aPhone = theQuery.PhoneNumber.Trim();
            aQuery = aQuery.Where(c => c.PhoneNumber.Contains(aPhone));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.City))
        {
            var aCity = theQuery.City.Trim();
            aQuery = aQuery.Where(c => c.City.Contains(aCity));
        }

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(c => c.Status == aStatusValue);
        }

        aQuery = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "email" => theQuery.SortDescending ? aQuery.OrderByDescending(c => c.Email) : aQuery.OrderBy(c => c.Email),
            "city" => theQuery.SortDescending ? aQuery.OrderByDescending(c => c.City) : aQuery.OrderBy(c => c.City),
            _ => theQuery.SortDescending ? aQuery.OrderByDescending(c => c.FullName) : aQuery.OrderBy(c => c.FullName)
        };

        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aRows = await aQuery
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .Select(c => new { c.Id, c.FullName, c.Email, c.PhoneNumber, c.City, c.Status })
            .ToListAsync(theCancellationToken);

        var aItems = aRows
            .Select(r => new CustomerSummaryView(r.Id, r.FullName, r.Email, r.PhoneNumber, r.City, ((EntityStatus)r.Status).ToString()))
            .ToList();

        return new PagedResult<CustomerSummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }

    public async Task<PagedResult<TopCustomerView>> GetTopCustomersAsync(
        int thePage = 1,
        int thePageSize = 10,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(thePage, thePageSize);

        // JOIN Orders x Customers, GROUP BY customer. Only real sales count.
        var aQuery =
            from o in myAppDbContext.Orders.AsNoTracking()
            join c in myAppDbContext.Customers on o.CustomerId equals c.Id
            where o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped
            group new { o, c } by new { c.Id, c.FullName } into g
            select new TopCustomerView(
                g.Key.Id,
                g.Key.FullName,
                g.Count(),
                g.Sum(x => x.o.TotalAmount),
                g.Max(x => x.o.TotalCurrency) ?? "VND");

        // Total = number of distinct paying customers (i.e. GROUP BY buckets).
        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aItems = await aQuery
            .OrderByDescending(v => v.TotalSpent)
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToListAsync(theCancellationToken);

        return new PagedResult<TopCustomerView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
