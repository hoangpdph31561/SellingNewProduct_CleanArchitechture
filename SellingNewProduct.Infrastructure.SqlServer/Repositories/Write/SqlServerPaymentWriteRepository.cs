using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Interfaces.Outbound;
using SellingNewProduct.Infrastructure.SqlServer.Mapping;
using SellingNewProduct.Infrastructure.SqlServer.Persistence;

namespace SellingNewProduct.Infrastructure.SqlServer.Repositories.Write;

internal sealed class SqlServerPaymentWriteRepository : IPaymentWriteRepository
{
    private readonly AppDbContext myAppDbContext;

    public SqlServerPaymentWriteRepository(AppDbContext theAppDbContext)
    {
        myAppDbContext = theAppDbContext;
    }

    public async Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId, theCancellationToken);

        return aRecord is null ? null : PaymentMapper.ToDomain(aRecord);
    }

    public async Task<IReadOnlyList<Payment>> GetByOrderAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aRecords = await myAppDbContext.Payments
            .AsNoTracking()
            .Where(r => r.OrderId == theOrderId)
            .ToListAsync(theCancellationToken);

        return aRecords.Select(PaymentMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Payment thePayment, CancellationToken theCancellationToken = default)
    {
        myAppDbContext.Payments.Add(PaymentMapper.ToRecord(thePayment));
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Payment thePayment, CancellationToken theCancellationToken = default)
    {
        var aRecord = await myAppDbContext.Payments.FirstOrDefaultAsync(r => r.Id == thePayment.Id, theCancellationToken);

        if (aRecord is null)
        {
            return;
        }

        PaymentMapper.MapInto(aRecord, thePayment);
        await myAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
