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
/// MongoDB read side for employees. There is no JOIN, so the per-employee order count is
/// computed by loading the real-sale orders once and grouping them in memory. Text filters
/// and sorting also happen in memory. Soft-deleted rows are excluded explicitly.
/// </summary>
internal sealed class MongoEmployeeQueries : IEmployeeQueries
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoEmployeeQueries(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<EmployeeSummaryView?> GetByIdAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default)
    {
        var aEmployee = await myMongoAppDbContext.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == theEmployeeId && e.Status != DeletedStatus, theCancellationToken);

        if (aEmployee is null)
        {
            return null;
        }

        var aOrderCount = await myMongoAppDbContext.Orders.AsNoTracking()
            .CountAsync(o => o.EmployeeId == theEmployeeId && o.Status != DeletedStatus &&
                             (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped),
                        theCancellationToken);

        return ToSummary(aEmployee, aOrderCount);
    }

    public async Task<PagedResult<EmployeeSummaryView>> SearchAsync(
        EmployeeSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myMongoAppDbContext.Employees.AsNoTracking()
            .Where(e => e.Status != DeletedStatus);

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(e => e.Status == aStatusValue);
        }

        var aCandidates = await aQuery.ToListAsync(theCancellationToken);

        IEnumerable<EmployeeDocument> aFiltered = aCandidates;

        if (!string.IsNullOrWhiteSpace(theQuery.Name))
        {
            var aName = theQuery.Name.Trim();
            aFiltered = aFiltered.Where(e => e.FullName.Contains(aName, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.Position))
        {
            var aPosition = theQuery.Position.Trim();
            aFiltered = aFiltered.Where(e => e.Position.Contains(aPosition, StringComparison.OrdinalIgnoreCase));
        }

        aFiltered = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "position" => theQuery.SortDescending ? aFiltered.OrderByDescending(e => e.Position) : aFiltered.OrderBy(e => e.Position),
            "hiredate" => theQuery.SortDescending ? aFiltered.OrderByDescending(e => e.HireDate) : aFiltered.OrderBy(e => e.HireDate),
            _ => theQuery.SortDescending ? aFiltered.OrderByDescending(e => e.FullName) : aFiltered.OrderBy(e => e.FullName)
        };

        var aMatched = aFiltered.ToList();

        var aPageDocs = aMatched
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToList();

        // Order counts for this page's employees: group the real-sale orders in memory.
        var aEmployeeIds = aPageDocs.Select(e => e.Id).ToList();
        var aOrders = await myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus &&
                        (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped) &&
                        aEmployeeIds.Contains(o.EmployeeId))
            .ToListAsync(theCancellationToken);

        var aCountByEmployee = aOrders
            .GroupBy(o => o.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Count());

        var aItems = aPageDocs
            .Select(e => ToSummary(e, aCountByEmployee.TryGetValue(e.Id, out var aCount) ? aCount : 0))
            .ToList();

        return new PagedResult<EmployeeSummaryView>(aItems, aPage.Page, aPage.PageSize, aMatched.Count);
    }

    private static EmployeeSummaryView ToSummary(EmployeeDocument theDoc, int theTotalOrders) =>
        new(theDoc.Id, theDoc.FullName, theDoc.Position, theDoc.HireDate, theTotalOrders, ((EntityStatus)theDoc.Status).ToString());
}
