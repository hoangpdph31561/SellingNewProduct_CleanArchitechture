using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Write;

internal sealed class SqlServerCustomerWriteRepository : ICustomerWriteRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerCustomerWriteRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<Customer?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : CustomerMapper.ToDomain(aRecord);
    }

    public async Task AddAsync(Customer theCustomer, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Customers.Add(CustomerMapper.ToRecord(theCustomer));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Customer theCustomer, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Customers.FirstOrDefaultAsync(r => r.Id == theCustomer.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        CustomerMapper.MapInto(aRecord, theCustomer);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task DeleteAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Customers.FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);
        if (aRecord == null)
        {
            return;
        }

        var aDomain = CustomerMapper.ToDomain(aRecord);
        aDomain.Delete();
        CustomerMapper.MapInto(aRecord, aDomain);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
