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
/// SQL read side for orders in the saga provider. Orders/OrderDetails/Payments live in SQL, but
/// the customer and employee NAMES needed by the read models live in MongoDB. Since you cannot
/// JOIN across the two stores, this repository aggregates the order data on SQL and then enriches
/// it in memory with names fetched through <see cref="ICrossDbDirectory"/> (which reads MongoDB).
/// This is the inherent cost of splitting aggregates across databases (polyglot CQRS).
/// </summary>
internal sealed class SqlOrderReadRepository : IOrderReadRepository
{
    private readonly AppDbContext myAppDbContext;
    private readonly ICrossDbDirectory myDirectory;

    public SqlOrderReadRepository(AppDbContext theAppDbContext, ICrossDbDirectory theDirectory)
    {
        myAppDbContext = theAppDbContext;
        myDirectory = theDirectory;
    }

    public async Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Orders
            .AsNoTracking()
            .Include(r => r.Details)
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : OrderMapper.ToDomain(aRecord);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Orders
            .AsNoTracking()
            .Include(r => r.Details)
            .Where(r => r.CustomerId == theCustomerId)
            .ToListAsync(theCancellationToken);

        return aRecords.Select(OrderMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime theFromUtc, DateTime theToUtc, CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Orders
            .AsNoTracking()
            .Include(r => r.Details)
            .Where(r => r.OrderDate >= theFromUtc && r.OrderDate <= theToUtc)
            .ToListAsync(theCancellationToken);

        return aRecords.Select(OrderMapper.ToDomain).ToList();
    }

    public async Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aHeader = await myAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Id == theOrderId)
            .Select(o => new
            {
                o.Id,
                o.CustomerId,
                o.EmployeeId,
                o.OrderStatus,
                o.OrderDate,
                o.TotalAmount,
                o.TotalCurrency
            })
            .FirstOrDefaultAsync(theCancellationToken);

        if (aHeader is null)
        {
            return null;
        }

        var aLines = await myAppDbContext.OrderDetails.AsNoTracking()
            .Where(d => d.OrderId == theOrderId)
            .Select(d => new OrderLineView(
                d.Id,
                d.ProductId,
                d.ProductName,
                d.UnitPriceAmount,
                d.Quantity,
                d.UnitPriceAmount * d.Quantity))
            .ToListAsync(theCancellationToken);

        var aAmountPaid = await myAppDbContext.Payments.AsNoTracking()
            .Where(p => p.OrderId == theOrderId && p.PaymentStatus == (int)PaymentStatus.Completed)
            .SumAsync(p => (decimal?)p.Amount, theCancellationToken) ?? 0m;

        var aCustomerName = await myDirectory.GetCustomerNameAsync(aHeader.CustomerId, theCancellationToken) ?? string.Empty;
        var aEmployeeName = await myDirectory.GetEmployeeNameAsync(aHeader.EmployeeId, theCancellationToken) ?? string.Empty;

        return new OrderDetailView(
            aHeader.Id,
            aHeader.CustomerId,
            aCustomerName,
            aHeader.EmployeeId,
            aEmployeeName,
            ((OrderStatus)aHeader.OrderStatus).ToString(),
            aHeader.OrderDate,
            aHeader.TotalAmount,
            aHeader.TotalCurrency,
            aAmountPaid,
            aLines);
    }

    public async Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aCustomerName = await myDirectory.GetCustomerNameAsync(theCustomerId, theCancellationToken);
        if (aCustomerName is null)
        {
            return null;
        }

        var aRows = await myAppDbContext.Orders.AsNoTracking()
            .Where(o => o.CustomerId == theCustomerId)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.Id,
                o.OrderDate,
                o.OrderStatus,
                o.EmployeeId,
                o.TotalAmount,
                o.TotalCurrency
            })
            .ToListAsync(theCancellationToken);

        var aEmployeeNameById = await myDirectory.GetEmployeeNamesAsync(theCancellationToken);

        var aOrders = aRows
            .Select(r => new CustomerOrderItemView(
                r.Id,
                r.OrderDate,
                ((OrderStatus)r.OrderStatus).ToString(),
                aEmployeeNameById.GetValueOrDefault(r.EmployeeId, string.Empty),
                r.TotalAmount,
                r.TotalCurrency))
            .ToList();

        var aTotalSpent = aRows
            .Where(r => r.OrderStatus == (int)OrderStatus.Confirmed || r.OrderStatus == (int)OrderStatus.Shipped)
            .Sum(r => r.TotalAmount);

        var aCurrency = aRows.FirstOrDefault()?.TotalCurrency ?? "VND";

        return new CustomerOrderHistoryView(
            theCustomerId,
            aCustomerName,
            aOrders.Count,
            aTotalSpent,
            aCurrency,
            aOrders);
    }

    public async Task<PagedResult<OrderSummaryView>> SearchAsync(
        OrderSearchQuery theQuery,
        CancellationToken theCancellationToken = default)
    {
        var aPage = new PageRequest(theQuery.Page, theQuery.PageSize);

        // Id-based filters run on SQL; name filters/sorts are applied in memory after enrichment.
        var aQuery = myAppDbContext.Orders.AsNoTracking();

        if (theQuery.CustomerId is not null)
        {
            aQuery = aQuery.Where(o => o.CustomerId == theQuery.CustomerId);
        }

        if (theQuery.EmployeeId is not null)
        {
            aQuery = aQuery.Where(o => o.EmployeeId == theQuery.EmployeeId);
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

        var aRows = await aQuery
            .Select(o => new
            {
                o.Id,
                o.CustomerId,
                o.EmployeeId,
                o.OrderStatus,
                o.OrderDate,
                o.TotalAmount,
                o.TotalCurrency
            })
            .ToListAsync(theCancellationToken);

        var aCustomerNameById = await myDirectory.GetCustomerNamesAsync(theCancellationToken);
        var aEmployeeNameById = await myDirectory.GetEmployeeNamesAsync(theCancellationToken);

        var aEnriched = aRows
            .Select(r => new
            {
                r.Id,
                CustomerName = aCustomerNameById.GetValueOrDefault(r.CustomerId, string.Empty),
                EmployeeName = aEmployeeNameById.GetValueOrDefault(r.EmployeeId, string.Empty),
                r.OrderStatus,
                r.OrderDate,
                r.TotalAmount,
                r.TotalCurrency
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(theQuery.CustomerName))
        {
            var aName = theQuery.CustomerName.Trim();
            aEnriched = aEnriched.Where(x => x.CustomerName.Contains(aName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(theQuery.EmployeeName))
        {
            var aName = theQuery.EmployeeName.Trim();
            aEnriched = aEnriched.Where(x => x.EmployeeName.Contains(aName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var aSorted = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "totalamount" => theQuery.SortDescending ? aEnriched.OrderByDescending(x => x.TotalAmount) : aEnriched.OrderBy(x => x.TotalAmount),
            "customername" => theQuery.SortDescending ? aEnriched.OrderByDescending(x => x.CustomerName) : aEnriched.OrderBy(x => x.CustomerName),
            "employeename" => theQuery.SortDescending ? aEnriched.OrderByDescending(x => x.EmployeeName) : aEnriched.OrderBy(x => x.EmployeeName),
            "orderdate" => theQuery.SortDescending ? aEnriched.OrderByDescending(x => x.OrderDate) : aEnriched.OrderBy(x => x.OrderDate),
            _ => aEnriched.OrderByDescending(x => x.OrderDate)
        };

        var aOrdered = aSorted.ToList();
        var aTotalCount = aOrdered.Count;

        var aItems = aOrdered
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .Select(r => new OrderSummaryView(
                r.Id,
                r.CustomerName,
                r.EmployeeName,
                ((OrderStatus)r.OrderStatus).ToString(),
                r.OrderDate,
                r.TotalAmount,
                r.TotalCurrency))
            .ToList();

        return new PagedResult<OrderSummaryView>(aItems, aPage.Page, aPage.PageSize, aTotalCount);
    }

    public async Task<IReadOnlyList<OrderStatusCountView>> GetStatusBreakdownAsync(CancellationToken theCancellationToken = default)
    {
        var aRows = await myAppDbContext.Orders.AsNoTracking()
            .GroupBy(o => o.OrderStatus)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(o => o.TotalAmount) })
            .ToListAsync(theCancellationToken);

        var aByStatus = aRows.ToDictionary(r => r.Status);

        return Enum.GetValues<OrderStatus>()
            .OrderBy(s => (int)s)
            .Select(s => aByStatus.TryGetValue((int)s, out var aRow)
                ? new OrderStatusCountView(s.ToString(), aRow.Count, aRow.Total)
                : new OrderStatusCountView(s.ToString(), 0, 0m))
            .ToList();
    }
}
