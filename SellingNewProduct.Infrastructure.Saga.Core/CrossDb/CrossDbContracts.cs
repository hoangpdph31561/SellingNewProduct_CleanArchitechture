namespace SellingNewProduct.Infrastructure.Saga.Core.CrossDb;

/// <summary>
/// Narrow "leaf" ports used to stitch read models together across the two databases WITHOUT
/// creating a dependency cycle.
///
/// The problem: in the saga (hybrid) provider the SQL order read models need customer/employee
/// names that live in MongoDB, while the MongoDB people read models need order statistics that
/// live in SQL. If each side depended on the other's full domain read port, the DI graph would be
/// circular. These two ports break the cycle because each implementation depends only on ITS OWN
/// database context: <see cref="ICrossDbDirectory"/> reads MongoDB only, <see cref="ICrossDbOrderStats"/>
/// reads SQL only.
/// </summary>
public interface ICrossDbDirectory
{
    Task<string?> GetCustomerNameAsync(Guid theCustomerId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetCustomerNamesAsync(CancellationToken theCancellationToken = default);

    Task<string?> GetEmployeeNameAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetEmployeeNamesAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<EmployeeInfo>> GetEmployeesAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(CancellationToken theCancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetCategoryNamesAsync(CancellationToken theCancellationToken = default);
}

/// <summary>SQL-backed order statistics needed by the MongoDB people read models.</summary>
public interface ICrossDbOrderStats
{
    Task<int> CountSalesOrdersForEmployeeAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> CountSalesOrdersByEmployeesAsync(
        IReadOnlyCollection<Guid> theEmployeeIds,
        CancellationToken theCancellationToken = default);

    Task<IReadOnlyList<CustomerOrderTotal>> GetCustomerOrderTotalsAsync(CancellationToken theCancellationToken = default);
}

public sealed record EmployeeInfo(Guid Id, string Name, string Position);

public sealed record CatalogProduct(Guid Id, string Name, Guid CategoryId, int StockQuantity);

public sealed record CustomerOrderTotal(Guid CustomerId, int TotalOrders, decimal TotalSpent, string Currency);
