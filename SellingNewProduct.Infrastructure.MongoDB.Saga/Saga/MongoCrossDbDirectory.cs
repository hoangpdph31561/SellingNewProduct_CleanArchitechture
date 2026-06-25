using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Persistence;
using SellingNewProduct.Infrastructure.Saga.Core.CrossDb;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Saga;

/// <summary>
/// MongoDB-backed implementation of <see cref="ICrossDbDirectory"/>: the catalogue/people lookups
/// that the SQL order read models need (those aggregates live in MongoDB here). It depends only on
/// <see cref="MongoReadDbContext"/> — never on a SQL read port — which keeps the cross-store read
/// graph free of dependency cycles.
/// </summary>
internal sealed class MongoCrossDbDirectory : ICrossDbDirectory
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoReadDbContext myMongoReadDbContext;

    public MongoCrossDbDirectory(MongoReadDbContext theMongoReadDbContext)
    {
        myMongoReadDbContext = theMongoReadDbContext;
    }

    public async Task<string?> GetCustomerNameAsync(Guid theCustomerId, CancellationToken theCancellationToken = default)
    {
        var aCustomer = await myMongoReadDbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == theCustomerId && c.Status != DeletedStatus, theCancellationToken);

        return aCustomer?.FullName;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetCustomerNamesAsync(CancellationToken theCancellationToken = default)
    {
        var aCustomers = await myMongoReadDbContext.Customers.AsNoTracking()
            .Where(c => c.Status != DeletedStatus)
            .Select(c => new { c.Id, c.FullName })
            .ToListAsync(theCancellationToken);

        return aCustomers.ToDictionary(c => c.Id, c => c.FullName);
    }

    public async Task<string?> GetEmployeeNameAsync(Guid theEmployeeId, CancellationToken theCancellationToken = default)
    {
        var aEmployee = await myMongoReadDbContext.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == theEmployeeId && e.Status != DeletedStatus, theCancellationToken);

        return aEmployee?.FullName;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetEmployeeNamesAsync(CancellationToken theCancellationToken = default)
    {
        var aEmployees = await myMongoReadDbContext.Employees.AsNoTracking()
            .Where(e => e.Status != DeletedStatus)
            .Select(e => new { e.Id, e.FullName })
            .ToListAsync(theCancellationToken);

        return aEmployees.ToDictionary(e => e.Id, e => e.FullName);
    }

    public async Task<IReadOnlyList<EmployeeInfo>> GetEmployeesAsync(CancellationToken theCancellationToken = default)
    {
        var aEmployees = await myMongoReadDbContext.Employees.AsNoTracking()
            .Where(e => e.Status != DeletedStatus)
            .Select(e => new { e.Id, e.FullName, e.Position })
            .ToListAsync(theCancellationToken);

        return aEmployees.Select(e => new EmployeeInfo(e.Id, e.FullName, e.Position)).ToList();
    }

    public async Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(CancellationToken theCancellationToken = default)
    {
        var aProducts = await myMongoReadDbContext.Products.AsNoTracking()
            .Where(p => p.Status != DeletedStatus)
            .Select(p => new { p.Id, p.Name, p.CategoryId, p.StockQuantity })
            .ToListAsync(theCancellationToken);

        return aProducts.Select(p => new CatalogProduct(p.Id, p.Name, p.CategoryId, p.StockQuantity)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetCategoryNamesAsync(CancellationToken theCancellationToken = default)
    {
        var aCategories = await myMongoReadDbContext.Categories.AsNoTracking()
            .Where(c => c.Status != DeletedStatus)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(theCancellationToken);

        return aCategories.ToDictionary(c => c.Id, c => c.Name);
    }
}
