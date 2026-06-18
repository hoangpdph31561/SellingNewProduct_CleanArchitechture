using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Read;

/// <summary>
/// SQL Server read side for employees. The order count per employee is a correlated subquery
/// (COUNT over Orders) pushed into the same SQL statement. Soft-deleted rows are excluded by
/// the Global Query Filter.
/// </summary>
internal sealed class SqlServerEmployeeReadRepository : IEmployeeReadRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerEmployeeReadRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : EmployeeMapper.ToDomain(aRecord);
    }

    public async Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Employees.AsNoTracking().ToListAsync(theCancellationToken);
        return aRecords.Select(EmployeeMapper.ToDomain).ToList();
    }

    public async Task<EmployeeSummaryView?> GetSummaryByIdAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default)
    {
        var aRow = await myAppDbContext.Employees.AsNoTracking()
            .Where(e => e.Id == theEmployeeId)
            .Select(e => new
            {
                e.Id,
                e.FullName,
                e.Position,
                e.HireDate,
                e.Status,
                TotalOrders = myAppDbContext.Orders.Count(o => o.EmployeeId == e.Id &&
                    (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped))
            })
            .FirstOrDefaultAsync(theCancellationToken);

        return aRow is null
            ? null
            : new EmployeeSummaryView(aRow.Id, aRow.FullName, aRow.Position, aRow.HireDate, aRow.TotalOrders, ((EntityStatus)aRow.Status).ToString());
    }

    public async Task<PagedResult<EmployeeSummaryView>> SearchAsync(
        EmployeeSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        var aQuery = myAppDbContext.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(theQuery.Name))
        {
            var aName = theQuery.Name.Trim();
            aQuery = aQuery.Where(e => e.FullName.Contains(aName));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.Position))
        {
            var aPosition = theQuery.Position.Trim();
            aQuery = aQuery.Where(e => e.Position.Contains(aPosition));
        }

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(e => e.Status == aStatusValue);
        }

        aQuery = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "position" => theQuery.SortDescending ? aQuery.OrderByDescending(e => e.Position) : aQuery.OrderBy(e => e.Position),
            "hiredate" => theQuery.SortDescending ? aQuery.OrderByDescending(e => e.HireDate) : aQuery.OrderBy(e => e.HireDate),
            _ => theQuery.SortDescending ? aQuery.OrderByDescending(e => e.FullName) : aQuery.OrderBy(e => e.FullName)
        };

        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aRows = await aQuery
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .Select(e => new
            {
                e.Id,
                e.FullName,
                e.Position,
                e.HireDate,
                e.Status,
                TotalOrders = myAppDbContext.Orders.Count(o => o.EmployeeId == e.Id &&
                    (o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped))
            })
            .ToListAsync(theCancellationToken);

        var aItems = aRows
            .Select(r => new EmployeeSummaryView(r.Id, r.FullName, r.Position, r.HireDate, r.TotalOrders, ((EntityStatus)r.Status).ToString()))
            .ToList();

        return new PagedResult<EmployeeSummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }
}
