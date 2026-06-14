using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories;

internal sealed class MongoCustomerRepository : ICustomerRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoCustomerRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : CustomerMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Customers
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(CustomerMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Customer theCustomer, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Customers.Add(CustomerMapper.ToDocument(theCustomer));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Customer theCustomer, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Customers.FirstOrDefaultAsync(r => r.Id == theCustomer.Id, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        CustomerMapper.MapInto(aDocument, theCustomer);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Customers.FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        var aDomain = CustomerMapper.ToDomain(aDocument);
        aDomain.Delete();
        CustomerMapper.MapInto(aDocument, aDomain);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
