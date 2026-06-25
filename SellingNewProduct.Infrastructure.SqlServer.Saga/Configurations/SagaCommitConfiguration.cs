using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Configurations;

internal sealed class SagaCommitConfiguration : IEntityTypeConfiguration<SagaCommitRecord>
{
    public void Configure(EntityTypeBuilder<SagaCommitRecord> theBuilder)
    {
        theBuilder.ToTable("SagaCommits");
        theBuilder.HasKey(x => x.SagaId);
        theBuilder.Property(x => x.CommittedUtc).IsRequired();
    }
}
