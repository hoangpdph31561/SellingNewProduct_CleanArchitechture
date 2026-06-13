using Microsoft.EntityFrameworkCore;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Domain.Payments;
using SellingNewProduct.Domain.Repositories;
using SellingNewProduct.Infrastructure.MongoDB.Mapping;
using SellingNewProduct.Infrastructure.MongoDB.Persistence;

namespace SellingNewProduct.Infrastructure.MongoDB.Repositories;

internal sealed class MongoPaymentRepository : IPaymentRepository
{
    private const int DeletedStatus = (int)EntityStatus.Deleted;

    private readonly MongoAppDbContext myMongoAppDbContext;

    public MongoPaymentRepository(MongoAppDbContext theMongoAppDbContext)
    {
        myMongoAppDbContext = theMongoAppDbContext;
    }

    public async Task<Payment?> GetByIdAsync(Guid theId, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == theId && r.Status != DeletedStatus, theCancellationToken);

        return aDocument is null ? null : PaymentMapper.ToDomain(aDocument);
    }

    public async Task<IReadOnlyList<Payment>> GetByOrderAsync(Guid theOrderId, CancellationToken theCancellationToken = default)
    {
        var aDocuments = await myMongoAppDbContext.Payments
            .AsNoTracking()
            .Where(r => r.OrderId == theOrderId && r.Status != DeletedStatus)
            .ToListAsync(theCancellationToken);

        return aDocuments.Select(PaymentMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Payment thePayment, CancellationToken theCancellationToken = default)
    {
        myMongoAppDbContext.Payments.Add(PaymentMapper.ToDocument(thePayment));
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }

    public async Task UpdateAsync(Payment thePayment, CancellationToken theCancellationToken = default)
    {
        var aDocument = await myMongoAppDbContext.Payments.FirstOrDefaultAsync(r => r.Id == thePayment.Id, theCancellationToken);

        if (aDocument is null)
        {
            return;
        }

        PaymentMapper.MapInto(aDocument, thePayment);
        await myMongoAppDbContext.SaveChangesAsync(theCancellationToken);
    }
}
