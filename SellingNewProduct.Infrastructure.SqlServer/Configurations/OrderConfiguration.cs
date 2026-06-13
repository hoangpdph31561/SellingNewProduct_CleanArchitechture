using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<OrderRecord>
{
    public void Configure(EntityTypeBuilder<OrderRecord> theBuilder)
    {
        theBuilder.ToTable("Orders");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.OrderStatus).IsRequired();
        theBuilder.Property(x => x.OrderDate).IsRequired();

        theBuilder.Property(x => x.ShippingStreet).HasMaxLength(300).IsRequired();
        theBuilder.Property(x => x.ShippingWard).HasMaxLength(100);
        theBuilder.Property(x => x.ShippingDistrict).HasMaxLength(100);
        theBuilder.Property(x => x.ShippingCity).HasMaxLength(100).IsRequired();
        theBuilder.Property(x => x.ShippingCountry).HasMaxLength(100).IsRequired();

        theBuilder.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
        theBuilder.Property(x => x.TotalCurrency).HasMaxLength(3).IsRequired();

        // Order owns its details: one-to-many, cascade delete (same aggregate).
        theBuilder.HasMany(x => x.Details)
                  .WithOne()
                  .HasForeignKey(d => d.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

        // Cross-aggregate references by id -> foreign keys, no cascade.
        theBuilder.HasOne<CustomerRecord>()
                  .WithMany()
                  .HasForeignKey(x => x.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

        theBuilder.HasOne<EmployeeRecord>()
                  .WithMany()
                  .HasForeignKey(x => x.EmployeeId)
                  .OnDelete(DeleteBehavior.Restrict);

        theBuilder.HasQueryFilter(x => x.Status != (int)EntityStatus.Deleted);
    }
}
