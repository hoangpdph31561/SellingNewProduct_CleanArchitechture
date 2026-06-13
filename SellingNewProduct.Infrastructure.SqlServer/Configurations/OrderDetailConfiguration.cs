using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Configurations;

internal sealed class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetailRecord>
{
    public void Configure(EntityTypeBuilder<OrderDetailRecord> theBuilder)
    {
        theBuilder.ToTable("OrderDetails");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        theBuilder.Property(x => x.UnitPriceAmount).HasColumnType("decimal(18,2)");
        theBuilder.Property(x => x.UnitPriceCurrency).HasMaxLength(3).IsRequired();
        theBuilder.Property(x => x.Quantity).IsRequired();

        theBuilder.HasIndex(x => x.OrderId);

        // Reference the product by id (foreign key, no cascade).
        theBuilder.HasOne<ProductRecord>()
                  .WithMany()
                  .HasForeignKey(x => x.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
    }
}
