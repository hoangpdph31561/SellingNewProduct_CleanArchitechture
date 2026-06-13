using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerRecord>
{
    public void Configure(EntityTypeBuilder<CustomerRecord> theBuilder)
    {
        theBuilder.ToTable("Customers");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        theBuilder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        theBuilder.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();

        theBuilder.Property(x => x.Street).HasMaxLength(300).IsRequired();
        theBuilder.Property(x => x.Ward).HasMaxLength(100);
        theBuilder.Property(x => x.District).HasMaxLength(100);
        theBuilder.Property(x => x.City).HasMaxLength(100).IsRequired();
        theBuilder.Property(x => x.Country).HasMaxLength(100).IsRequired();

        theBuilder.HasQueryFilter(x => x.Status != (int)EntityStatus.Deleted);
    }
}
