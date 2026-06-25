using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Configurations;

internal sealed class SagaTransactionConfiguration : IEntityTypeConfiguration<SagaTransactionRecord>
{
    public void Configure(EntityTypeBuilder<SagaTransactionRecord> theBuilder)
    {
        theBuilder.ToTable("SagaTransactions");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.Status).IsRequired();
        theBuilder.Property(x => x.StartedUtc).IsRequired();
        theBuilder.Property(x => x.Steps).HasMaxLength(1000);
    }
}
