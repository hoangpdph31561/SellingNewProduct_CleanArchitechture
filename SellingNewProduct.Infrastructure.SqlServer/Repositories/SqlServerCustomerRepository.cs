using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Customers;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories;

internal sealed class SqlServerCustomerRepository : ICustomerRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerCustomerRepository(AppDbContext theAppDbContext)
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

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Customers.AsNoTracking().ToListAsync(theCancellationToken);
        return aRecords.Select(CustomerMapper.ToDomain).ToList();
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
}
