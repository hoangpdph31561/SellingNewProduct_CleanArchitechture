using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Application.Queries;
using SellingNewProduct.Application.ReadModels;
using SellingNewProduct.Domain.Orders;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Queries;

/// <summary>
/// SQL Server read side, implemented with real JOINs — EF Core translates the LINQ
/// below into a single SQL statement with JOINs that runs on the database (it does
/// not pull whole tables into the app). This class lives INSIDE the SqlServer
/// assembly so it can read the <c>internal DbSet</c>s.
/// Soft-delete: the tables have a Global Query Filter (Status != Deleted), so
/// deleted rows are excluded automatically.
/// </summary>
internal sealed class SqlServerOrderQueries : IOrderQueries
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerOrderQueries(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<OrderDetailView?> GetOrderDetailAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        // One JOIN over three tables to get the header + customer name + employee name.
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

        // Total amount paid: SUM on the database, counting only completed payments.
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

        // JOIN Orders x Employees so every order already carries the selling employee's name.
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

        // Total spent counts only real sales (Confirmed/Shipped); excludes Draft & Cancelled.
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

    public async Task<IReadOnlyList<OrderSummaryView>> SearchAsync(
        Guid? theCustomerId = null,
        Guid? theEmployeeId = null,
        OrderStatus? theStatus = null,
        DateTime? theFromUtc = null,
        DateTime? theToUtc = null,
        CancellationToken theCancellationToken = default)
    {
        // Start from a JOIN over three tables, then append filters when a parameter has a value.
        var aQuery =
            from o in myAppDbContext.Orders.AsNoTracking()
            join c in myAppDbContext.Customers on o.CustomerId equals c.Id
            join e in myAppDbContext.Employees on o.EmployeeId equals e.Id
            select new { o, CustomerName = c.FullName, EmployeeName = e.FullName };

        if (theCustomerId is not null)
        {
            aQuery = aQuery.Where(x => x.o.CustomerId == theCustomerId);
        }

        if (theEmployeeId is not null)
        {
            aQuery = aQuery.Where(x => x.o.EmployeeId == theEmployeeId);
        }

        if (theStatus is not null)
        {
            var aStatusValue = (int)theStatus.Value;
            aQuery = aQuery.Where(x => x.o.OrderStatus == aStatusValue);
        }

        if (theFromUtc is not null)
        {
            aQuery = aQuery.Where(x => x.o.OrderDate >= theFromUtc);
        }

        if (theToUtc is not null)
        {
            aQuery = aQuery.Where(x => x.o.OrderDate <= theToUtc);
        }

        var aRows = await aQuery
            .OrderByDescending(x => x.o.OrderDate)
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

        return aRows
            .Select(r => new OrderSummaryView(
                r.Id,
                r.CustomerName,
                r.EmployeeName,
                ((OrderStatus)r.OrderStatus).ToString(),
                r.OrderDate,
                r.TotalAmount,
                r.TotalCurrency))
            .ToList();
    }
}
