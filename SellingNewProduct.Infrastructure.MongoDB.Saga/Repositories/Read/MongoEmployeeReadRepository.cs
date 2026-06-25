using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Repositories.Read;

/// <summary>
/// MongoDB read side for employees. Employee documents live in MongoDB, but the per-employee order
/// count is an ORDER statistic that lives in SQL, fetched through <see cref="ICrossDbOrderStats"/>.
/// Text filters and sorting run in memory.
/// </summary>
internal sealed class MongoEmployeeReadRepository : IEmployeeReadRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoReadDbContext myMongoReadDbContext;
    private readonly ICrossDbOrderStats myOrderStats;

    public MongoEmployeeReadRepository(MongoReadDbContext theMongoReadDbContext, ICrossDbOrderStats theOrderStats)
    {
        myMongoReadDbContext = theMongoReadDbContext;
        myOrderStats = theOrderStats;
    }

    public async Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoReadDbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : EmployeeMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoReadDbContext.Employees
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(EmployeeMapper.ToDomain).ToList();
    }

    public async Task<EmployeeSummaryView?> GetSummaryByIdAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default)
    {
        var aEmployee = await myMongoReadDbContext.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == theEmployeeId && e.Status != DeletedStatus, theCancellationToken);

        if (aEmployee is null)
        {
            return null;
        }

        var aOrderCount = await myOrderStats.CountSalesOrdersForEmployeeAsync(theEmployeeId, theCancellationToken);
        return ToSummary(aEmployee, aOrderCount);
    }

    public async Task<PagedResult<EmployeeSummaryView>> SearchAsync(
        EmployeeSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myMongoReadDbContext.Employees.AsNoTracking()
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

        var aEmployeeIds = aPageDocs.Select(e => e.Id).ToList();
        var aCountByEmployee = await myOrderStats.CountSalesOrdersByEmployeesAsync(aEmployeeIds, theCancellationToken);

        var aItems = aPageDocs
            .Select(e => ToSummary(e, aCountByEmployee.GetValueOrDefault(e.Id, 0)))
            .ToList();

        return new PagedResult<EmployeeSummaryView>(aItems, aPage.Page, aPage.PageSize, aMatched.Count);
    }

    private static EmployeeSummaryView ToSummary(EmployeeDocument theDoc, int theTotalOrders) =>
        new(theDoc.Id, theDoc.FullName, theDoc.Position, theDoc.HireDate, theTotalOrders, ((EntityStatus)theDoc.Status).ToString());
}
