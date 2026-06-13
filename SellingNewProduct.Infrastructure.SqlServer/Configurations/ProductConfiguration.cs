using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<ProductRecord>
{
    public void Configure(EntityTypeBuilder<ProductRecord> theBuilder)
    {
        theBuilder.ToTable("Products");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        theBuilder.Property(x => x.Sku).HasMaxLength(50).IsRequired();
        theBuilder.HasIndex(x => x.Sku).IsUnique();

        theBuilder.Property(x => x.Color).HasMaxLength(50).IsRequired();
        theBuilder.Property(x => x.Size).IsRequired();
        theBuilder.Property(x => x.PriceAmount).HasColumnType("decimal(18,2)");
        theBuilder.Property(x => x.PriceCurrency).HasMaxLength(3).IsRequired();
        theBuilder.Property(x => x.StockQuantity).IsRequired();

        // Cross-aggregate reference by id, enforced in DB as a foreign key.
        theBuilder.HasOne<CategoryRecord>()
                  .WithMany()
                  .HasForeignKey(x => x.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

        theBuilder.HasQueryFilter(x => x.Status != (int)EntityStatus.Deleted);
    }
}
