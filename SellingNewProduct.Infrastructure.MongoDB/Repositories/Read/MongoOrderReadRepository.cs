using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Queries;
using SellingNewProduct.Domain.ReadModels;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories.Read;

/// <summary>
/// MongoDB read side for orders. No relational JOIN across collections, so we load the documents
/// we need and "stitch" them in memory with LINQ-to-objects. Same contract as SQL Server, only
/// the execution strategy differs. Soft-deleted rows are excluded explicitly.
/// </summary>
internal sealed class MongoOrderReadRepository : IOrderReadRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;
    private const int CompletedPayment = (int)PaymentStatus.Completed;

    private readonly MongoReadDbContext myMongoReadDbContext;

    public MongoOrderReadRepository(MongoReadDbContext theMongoReadDbContext)
    {
        myMongoReadDbContext = theMongoReadDbContext;
    }

    public async Task<Order?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoReadDbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : OrderMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoReadDbContext.Orders
            .AsNoTracking()
            .Where(r => r.CustomerId == theCustomerId && r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(OrderMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Order>> GetByDateRangeAsync(DateTime theFromUtc, DateTime theToUtc, CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoReadDbContext.Orders
            .AsNoTracking()
            .Where(r => r.OrderDate >= theFromUtc && r.OrderDate <= theToUtc && r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(OrderMapper.ToDomain).ToList();
    }

    public async Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aOrder = await myMongoReadDbContext.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == theOrderId && o.Status != DeletedStatus, theCancellationToken);

        if (aOrder is null)
        {
            return null;
        }

        var aCustomer = await myMongoReadDbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == aOrder.CustomerId, theCancellationToken);

        var aEmployee = await myMongoReadDbContext.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == aOrder.EmployeeId, theCancellationToken);

        var aPayments = await myMongoReadDbContext.Payments.AsNoTracking()
            .Where(p => p.OrderId == theOrderId && p.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        var aAmountPaid = aPayments
            .Where(p => p.PaymentStatus == CompletedPayment)
            .Sum(p => p.Amount);

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
        var aCustomer = await myMongoReadDbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == theCustomerId && c.Status != DeletedStatus, theCancellationToken);

        if (aCustomer is null)
        {
            return null;
        }

        var aOrders = await myMongoReadDbContext.Orders.AsNoTracking()
            .Where(o => o.CustomerId == theCustomerId && o.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        var aEmployeeIds = aOrders.Select(o => o.EmployeeId).Distinct().ToList();
        var aEmployees = await myMongoReadDbContext.Employees.AsNoTracking()
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

        var aCustomerIdFilter = await ResolveCustomerIdsByNameAsync(theQuery.CustomerName, theCancellationToken);
        var aEmployeeIdFilter = await ResolveEmployeeIdsByNameAsync(theQuery.EmployeeName, theCancellationToken);

        var aQuery = myMongoReadDbContext.Orders.AsNoTracking()
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

        aQuery = (theQuery.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "totalamount" => theQuery.SortDescending ? aQuery.OrderByDescending(o => o.TotalAmount) : aQuery.OrderBy(o => o.TotalAmount),
            "orderdate" => theQuery.SortDescending ? aQuery.OrderByDescending(o => o.OrderDate) : aQuery.OrderBy(o => o.OrderDate),
            _ => aQuery.OrderByDescending(o => o.OrderDate)
        };

        var aTotalCount = await aQuery.CountAsync(theCancellationToken);

        var aOrders = await aQuery
            .Skip(aPage.Skip)
            .Take(aPage.PageSize)
            .ToListAsync(theCancellationToken);

        var aCustomerIds = aOrders.Select(o => o.CustomerId).Distinct().ToList();
        var aEmployeeIds = aOrders.Select(o => o.EmployeeId).Distinct().ToList();

        var aCustomerNameById = (await myMongoReadDbContext.Customers.AsNoTracking()
            .Where(c => aCustomerIds.Contains(c.Id))
            .ToListAsync(theCancellationToken))
            .ToDictionary(c => c.Id, c => c.FullName);

        var aEmployeeNameById = (await myMongoReadDbContext.Employees.AsNoTracking()
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
        var aOrders = await myMongoReadDbContext.Orders.AsNoTracking()
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

    private async Task<List<Guid>?> ResolveCustomerIdsByNameAsync(string? theName, CancellationToken theCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(theName))
        {
            return null;
        }

        var aName = theName.Trim();
        var aCustomers = await myMongoReadDbContext.Customers.AsNoTracking()
            .Where(c => c.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aCustomers
            .Where(c => c.FullName.Contains(aName, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Id)
            .ToList();
    }

    private async Task<List<Guid>?> ResolveEmployeeIdsByNameAsync(string? theName, CancellationToken theCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(theName))
        {
            return null;
        }

        var aName = theName.Trim();
        var aEmployees = await myMongoReadDbContext.Employees.AsNoTracking()
            .Where(e => e.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aEmployees
            .Where(e => e.FullName.Contains(aName, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id)
            .ToList();
    }
}
