using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Read;

/// <summary>
/// SQL Server read side for orders. Implemented with real JOINs — EF Core translates the LINQ
/// into a single SQL statement. Soft-deleted rows are excluded by the Global Query Filter.
/// </summary>
internal sealed class SqlServerOrderReadRepository : IOrderReadRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerOrderReadRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
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
        var aHeader = await (
            from o in myAppDbContext.Orders.AsNoTracking()
            join c in myAppDbContext.Customers on o.CustomerId equals c.Id
            join e in myAppDbContext.Employees on o.EmployeeId equals e.Id
            where o.Id == theOrderId
            select new
            {
                o.Id,
                o.CustomerId,
                CustomerName = c.FullName,
                o.EmployeeId,
                EmployeeName = e.FullName,
                o.OrderStatus,
                o.OrderDate,
                o.TotalAmount,
                o.TotalCurrency
            }).FirstOrDefaultAsync(theCancellationToken);

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

        return new OrderDetailView(
            aHeader.Id,
            aHeader.CustomerId,
            aHeader.CustomerName,
            aHeader.EmployeeId,
            aHeader.EmployeeName,
            ((OrderStatus)aHeader.OrderStatus).ToString(),
            aHeader.OrderDate,
            aHeader.TotalAmount,
            aHeader.TotalCurrency,
            aAmountPaid,
            aLines);
    }

    public async Task<CustomerOrderHistoryView?> GetCustomerHistoryAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aCustomer = await myAppDbContext.Customers.AsNoTracking()
            .Where(c => c.Id == theCustomerId)
            .Select(c => new { c.Id, c.FullName })
            .FirstOrDefaultAsync(theCancellationToken);

        if (aCustomer is null)
        {
            return null;
        }

        var aRows = await (
            from o in myAppDbContext.Orders.AsNoTracking()
            join e in myAppDbContext.Employees on o.EmployeeId equals e.Id
            where o.CustomerId == theCustomerId
            orderby o.OrderDate descending
            select new
            {
                o.Id,
                o.OrderDate,
                o.OrderStatus,
                EmployeeName = e.FullName,
                o.TotalAmount,
                o.TotalCurrency
            }).ToListAsync(theCancellationToken);

        var aOrders = aRows
            .Select(r => new CustomerOrderItemView(
                r.Id,
                r.OrderDate,
                ((OrderStatus)r.OrderStatus).ToString(),
                r.EmployeeName,
                r.TotalAmount,
                r.TotalCurrency))
            .ToList();

        var aTotalSpent = aRows
            .Where(r => r.OrderStatus == (int)OrderStatus.Confirmed || r.OrderStatus == (int)OrderStatus.Shipped)
            .Sum(r => r.TotalAmount);

        var aCurrency = aRows.FirstOrDefault()?.TotalCurrency ?? "VND";

        return new CustomerOrderHistoryView(
            aCustomer.Id,
            aCustomer.FullName,
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

        var aQuery =
            from o in myAppDbContext.Orders.AsNoTracking()
            join c in myAppDbContext.Customers on o.CustomerId equals c.Id
            join e in myAppDbContext.Employees on o.EmployeeId equals e.Id
            select new { o, CustomerName = c.FullName, EmployeeName = e.FullName };

        if (theQuery.CustomerId is not null)
        {
            aQuery = aQuery.Where(x => x.o.CustomerId == theQuery.CustomerId);
        }

        if (theQuery.EmployeeId is not null)
        {
            aQuery = aQuery.Where(x => x.o.EmployeeId == theQuery.EmployeeId);
        }

        if (!string.IsNullOrWhiteSpace(theQuery.CustomerName))
        {
            var aCustomerName = theQuery.CustomerName.Trim();
            aQuery = aQuery.Where(x => x.CustomerName.Contains(aCustomerName));
        }

        if (!string.IsNullOrWhiteSpace(theQuery.EmployeeName))
        {
            var aEmployeeName = theQuery.EmployeeName.Trim();
            aQuery = aQuery.Where(x => x.EmployeeName.Contains(aEmployeeName));
        }

        if (theQuery.Status is not null)
        {
            var aStatusValue = (int)theQuery.Status.Value;
            aQuery = aQuery.Where(x => x.o.OrderStatus == aStatusValue);
        }

        if (theQuery.FromUtc is not null)
        {
            aQuery = aQuery.Where(x => x.o.OrderDate >= theQuery.FromUtc);
        }

        if (theQuery.ToUtc is not null)
        {
            aQuery = aQuery.Where(x => x.o.OrderDate <= theQuery.ToUtc);
        }

        aQuery = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "totalamount" => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.o.TotalAmount) : aQuery.OrderBy(x => x.o.TotalAmount),
            "customername" => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.CustomerName) : aQuery.OrderBy(x => x.CustomerName),
            "employeename" => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.EmployeeName) : aQuery.OrderBy(x => x.EmployeeName),
            "orderdate" => theQuery.SortDescending ? aQuery.OrderByDescending(x => x.o.OrderDate) : aQuery.OrderBy(x => x.o.OrderDate),
            _ => aQuery.OrderByDescending(x => x.o.OrderDate)
        };

        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aRows = await aQuery
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .Select(x => new
            {
                x.o.Id,
                x.CustomerName,
                x.EmployeeName,
                x.o.OrderStatus,
                x.o.OrderDate,
                x.o.TotalAmount,
                x.o.TotalCurrency
            })
            .ToListAsync(theCancellationToken);

        var aItems = aRows
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
