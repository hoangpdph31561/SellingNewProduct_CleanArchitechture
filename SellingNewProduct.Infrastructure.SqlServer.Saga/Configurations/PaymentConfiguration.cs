using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Saga.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> theBuilder)
    {
        theBuilder.ToTable("Payments");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        theBuilder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        theBuilder.Property(x => x.Method).IsRequired();
        theBuilder.Property(x => x.PaymentStatus).IsRequired();

        // Payment references Order, which IS a SQL table in this provider — keep the foreign key.
        theBuilder.HasOne<OrderRecord>()
                  .WithMany()
                  .HasForeignKey(x => x.OrderId)
                  .OnDelete(DeleteBehavior.Restrict);

        theBuilder.HasQueryFilter(x => x.Status != (int)EntityStatus.Deleted);
    }
}
