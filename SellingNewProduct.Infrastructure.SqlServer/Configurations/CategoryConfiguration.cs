using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<CategoryRecord>
{
    public void Configure(EntityTypeBuilder<CategoryRecord> theBuilder)
    {
        theBuilder.ToTable("Categories");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        theBuilder.Property(x => x.Description).HasMaxLength(1000);

        theBuilder.HasQueryFilter(x => x.Status != (int)EntityStatus.Deleted);
    }
}
