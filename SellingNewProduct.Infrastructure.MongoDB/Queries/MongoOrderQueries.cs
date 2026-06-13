using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
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

    public async Task<IReadOnlyList<OrderSummaryView>> SearchAsync(
        Guid? theCustomerId = null,
        Guid? theEmployeeId = null,
        OrderStatus? theStatus = null,
        DateTime? theFromUtc = null,
        DateTime? theToUtc = null,
        CancellationToken theCancellationToken = default)
    {
        var aQuery = myMongoAppDbContext.Orders.AsNoTracking()
            .Where(o => o.Status != DeletedStatus);

        if (theCustomerId is not null)
        {
            aQuery = aQuery.Where(o => o.CustomerId == theCustomerId);
        }

        if (theEmployeeId is not null)
        {
            aQuery = aQuery.Where(o => o.EmployeeId == theEmployeeId);
        }

        if (theStatus is not null)
        {
            var aStatusValue = (int)theStatus.Value;
            aQuery = aQuery.Where(o => o.OrderStatus == aStatusValue);
        }

        if (theFromUtc is not null)
        {
            aQuery = aQuery.Where(o => o.OrderDate >= theFromUtc);
        }

        if (theToUtc is not null)
        {
            aQuery = aQuery.Where(o => o.OrderDate <= theToUtc);
        }

        var aOrders = await aQuery.ToListAsync(theCancellationToken);

        // Stitch customer and employee names in memory.
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

        return aOrders
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderSummaryView(
                o.Id,
                aCustomerNameById.TryGetValue(o.CustomerId, out var aCName) ? aCName : "(unknown)",
                aEmployeeNameById.TryGetValue(o.EmployeeId, out var aEName) ? aEName : "(unknown)",
                ((OrderStatus)o.OrderStatus).ToString(),
                o.OrderDate,
                o.TotalAmount,
                o.TotalCurrency))
            .ToList();
    }
}
