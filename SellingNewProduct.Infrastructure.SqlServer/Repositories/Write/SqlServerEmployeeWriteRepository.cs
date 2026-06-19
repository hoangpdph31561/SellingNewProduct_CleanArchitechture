using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Employees;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Write;

internal sealed class SqlServerEmployeeWriteRepository : IEmployeeWriteRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerEmployeeWriteRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<Employee?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : EmployeeMapper.ToDomain(aRecord);
    }

    public async Task AddAsync(Employee theEmployee, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Employees.Add(EmployeeMapper.ToRecord(theEmployee));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Employee theEmployee, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Employees.FirstOrDefaultAsync(r => r.Id == theEmployee.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        EmployeeMapper.MapInto(aRecord, theEmployee);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
