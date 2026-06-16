using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Abstractions;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Queries;

/// <summary>
/// MongoDB read side. Unlike SQL Server, MongoDB has no relational JOIN across
/// collections. So we load the documents we need and "stitch" them together in
/// memory with LINQ-to-objects.
///
/// This illustrates the project's core thesis: for the same <see cref="IOrderQueries"/>,
/// SQL pushes the JOIN down to the database, while Mongo stitches at the application
/// layer (or denormalises the names ahead of time). The contract does not change —
/// only the execution strategy does.
/// Mongo has no Global Query Filter, so each query must filter Status != Deleted itself.
/// </summary>
internal sealed class MongoOrderQueries : IOrderQueries
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;
    private const int CompletedPayment = (int)PaymentStatus.Completed;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoOrderQueries(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await myMongoAppDbContext.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == theOrderId && o.Status != DeletedStatus, theCancellationToken);

        if (aOrder is null)
        {
            return null;
        }

        // No JOIN — look up each related document by id.
        var aCustomer = await myMongoAppDbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == aOrder.CustomerId, theCancellationToken);

        var aEmployee = await myMongoAppDbContext.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == aOrder.EmployeeId, theCancellationToken);

        var aPayments = await myMongoAppDbContext.Payments.AsNoTracking()
            .Where(p => p.OrderId == theOrderId && p.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        var aAmountPaid = aPayments
            .Where(p => p.PaymentStatus == CompletedPayment)
            .Sum(p => p.Amount);

        // Details are embedded in the order document — no extra query needed.
        var aLines = aOrder.Details
            .Select(d => new OrderLineView(
                d.Id,
                d.ProductId,
                d.ProductName,
                d.UnitPriceAmount,
                d.Quantity,
                d.UnitPriceAmount * d.Quantity))
            .ToList();

        return new OrderDetailView(
            aOrder.Id,
            aOrder.CustomerId,
            aCustomer?.FullName ?? "(unknown)",
            aOrder.EmployeeId,
            aEmployee?.FullName ?? "(unknown)",
            ((OrderStatus)aOrder.OrderStatus).ToString(),
            aOrder.OrderDate,
            aOrder.TotalAmount,
            aOrder.TotalCurrency,
            aAmountPaid,
            aLines);
    }

    public async Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aCustomer = await myMongoAppDbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == theCustomerId && c.Status != DeletedStatus, theCancellationToken);

        if (aCustomer is null)
        {
            return null;
        }

        var aOrders = await myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.CustomerId == theCustomerId && o.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        // Load the related employees, then stitch the names in memory (instead of a JOIN).
        var aEmployeeIds = aOrders.Select(o => o.EmployeeId).Distinct().ToList();
        var aEmployees = await myMongoAppDbContext.Employees.AsNoTracking()
            .Where(e => aEmployeeIds.Contains(e.Id))
            .ToListAsync(theCancellationToken);
        var aEmployeeNameById = aEmployees.ToDictionary(e => e.Id, e => e.FullName);

        var aItems = aOrders
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new CustomerOrderItemView(
                o.Id,
                o.OrderDate,
                ((OrderStatus)o.OrderStatus).ToString(),
                aEmployeeNameById.TryGetValue(o.EmployeeId, out var aName) ? aName : "(unknown)",
                o.TotalAmount,
                o.TotalCurrency))
            .ToList();

        var aTotalSpent = aOrders
            .Where(o => o.OrderStatus == (int)OrderStatus.Confirmed || o.OrderStatus == (int)OrderStatus.Shipped)
            .Sum(o => o.TotalAmount);

        var aCurrency = aOrders.FirstOrDefault()?.TotalCurrency ?? "VND";

        return new CustomerOrderHistoryView(
            aCustomer.Id,
            aCustomer.FullName,
            aItems.Count,
            aTotalSpent,
            aCurrency,
            aItems);
    }

    public async Task<PagedResult<OrderSummaryView>> SearchAsync(
        OrderSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        // Mongo cannot JOIN, so a filter on the customer/employee NAME has to be turned
        // into a filter on their IDs first: find the matching people, then keep orders
        // whose CustomerId/EmployeeId is in that set ($in). This keeps the orders query
        // — and therefore the paging — running on the database.
        var aCustomerIdFilter = await ResolveCustomerIdsByNameAsync(theQuery.CustomerName, theCancellationToken);
        var aEmployeeIdFilter = await ResolveEmployeeIdsByNameAsync(theQuery.EmployeeName, theCancellationToken);

        var aQuery = myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus);

        if (theQuery.CustomerId is not null)
        {
            aQuery = aQuery.Where(o => o.CustomerId == theQuery.CustomerId);
        }

        if (theQuery.EmployeeId is not null)
        {
            aQuery = aQuery.Where(o => o.EmployeeId == theQuery.EmployeeId);
        }

        if (aCustomerIdFilter is not null)
        {
            aQuery = aQuery.Where(o => aCustomerIdFilter.Contains(o.CustomerId));
        }

        if (aEmployeeIdFilter is not null)
        {
            aQuery = aQuery.Where(o => aEmployeeIdFilter.Contains(o.EmployeeId));
        }

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(o => o.OrderStatus == aStatusValue);
        }

        if (theQuery.FromUtc is not null)
        {
            aQuery = aQuery.Where(o => o.OrderDate >= theQuery.FromUtc);
        }

        if (theQuery.ToUtc is not null)
        {
            aQuery = aQuery.Where(o => o.OrderDate <= theQuery.ToUtc);
        }

        // Sort by the order's OWN fields on the database. Sorting by the stitched
        // customer/employee name would need every matching order loaded first (no JOIN),
        // so those fall back to the default newest-first — a concrete illustration of how
        // Mongo's lack of JOIN changes what the same contract can do efficiently.
        aQuery = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "totalamount" => theQuery.SortDescending ? aQuery.OrderByDescending(o => o.TotalAmount) : aQuery.OrderBy(o => o.TotalAmount),
            "orderdate" => theQuery.SortDescending ? aQuery.OrderByDescending(o => o.OrderDate) : aQuery.OrderBy(o => o.OrderDate),
            _ => aQuery.OrderByDescending(o => o.OrderDate)
        };

        // COUNT the whole filtered set first (so the UI knows the total), then fetch one page.
        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aOrders = await aQuery
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToListAsync(theCancellationToken);

        // Stitch customer and employee names in memory — only for this page's orders.
        var aCustomerIds = aOrders.Select(o => o.CustomerId).Distinct().ToList();
        var aEmployeeIds = aOrders.Select(o => o.EmployeeId).Distinct().ToList();

        var aCustomerNameById = (await myMongoAppDbContext.Customers.AsNoTracking()
            .Where(c => aCustomerIds.Contains(c.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(c => c.Id, c => c.FullName);

        var aEmployeeNameById = (await myMongoAppDbContext.Employees.AsNoTracking()
            .Where(e => aEmployeeIds.Contains(e.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(e => e.Id, e => e.FullName);

        var aItems = aOrders
            .Select(o => new OrderSummaryView(
                o.Id,
                aCustomerNameById.TryGetValue(o.CustomerId, out var aCName) ? aCName : "(unknown)",
                aEmployeeNameById.TryGetValue(o.EmployeeId, out var aEName) ? aEName : "(unknown)",
                ((OrderStatus)o.OrderStatus).ToString(),
                o.OrderDate,
                o.TotalAmount,
                o.TotalCurrency))
            .ToList();

        return new PagedResult<OrderSummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }

    public async Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default)
    {
        // Mongo cannot GROUP BY across the query here, so load the live orders and group
        // them in memory — the same status breakdown as the SQL version.
        var aOrders = await myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        var aByStatus = aOrders
            .GroupBy(o => o.OrderStatus)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(o => o.TotalAmount)));

        return Enum.GetValues<OrderStatus>()
            .OrderBy(s => (int)s)
            .Select(s => aByStatus.TryGetValue((int)s, out var aRow)
                ? new OrderStatusCountView(s.ToString(), aRow.Count, aRow.Total)
                : new OrderStatusCountView(s.ToString(), 0, 0m))
            .ToList();
    }

    /// <summary>
    /// Returns the ids of non-deleted customers whose name contains <paramref name="theName"/>,
    /// or <c>null</c> when no name filter was supplied (meaning "do not filter by customer name").
    /// An empty list means "name supplied but nobody matched" — the caller then returns no orders.
    /// </summary>
    private async Task<List<Guid>?> ResolveCustomerIdsByNameAsync(string? theName, CancellationToken theCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(theName))
        {
            return null;
        }

        var aName = theName.Trim();
        var aCustomers = await myMongoAppDbContext.Customers.AsNoTracking()
            .Where(c => c.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aCustomers
            .Where(c => c.FullName.Contains(aName, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Id)
            .ToList();
    }

    /// <summary>Same as <see cref="ResolveCustomerIdsByNameAsync"/> but for employees.</summary>
    private async Task<List<Guid>?> ResolveEmployeeIdsByNameAsync(string? theName, CancellationToken theCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(theName))
        {
            return null;
        }

        var aName = theName.Trim();
        var aEmployees = await myMongoAppDbContext.Employees.AsNoTracking()
            .Where(e => e.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aEmployees
            .Where(e => e.FullName.Contains(aName, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id)
            .ToList();
    }
}
