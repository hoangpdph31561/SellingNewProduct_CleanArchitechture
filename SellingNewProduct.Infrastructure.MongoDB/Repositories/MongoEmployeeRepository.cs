using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories;

internal sealed class MongoEmployeeRepository : IEmployeeRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoEmployeeRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : EmployeeMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Employees
            .AsNoTracking()
            .Where(r => r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(EmployeeMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Employee theEmployee, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Employees.Add(EmployeeMapper.ToDocument(theEmployee));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Employee theEmployee, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Employees.FirstOrDefaultAsync(r => r.Id == theEmployee.Id, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        EmployeeMapper.MapInto(aDocument, theEmployee);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
