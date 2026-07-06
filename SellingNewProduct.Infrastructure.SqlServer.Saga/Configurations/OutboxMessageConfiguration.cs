using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageRecord>
{
    public void Configure(EntityTypeBuilder<OutboxMessageRecord> theBuilder)
    {
        theBuilder.ToTable("OutboxMessages");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.Route).HasMaxLength(200).IsRequired();
        theBuilder.Property(x => x.MessageType).HasMaxLength(200).IsRequired();
        theBuilder.Property(x => x.Payload).IsRequired();
        theBuilder.Property(x => x.PartitionKey).HasMaxLength(100);

        // The dispatcher polls "not yet published, oldest first" — index that access path.
        theBuilder.HasIndex(x => new { x.ProcessedUtc, x.CreatedUtc });
    }
}
