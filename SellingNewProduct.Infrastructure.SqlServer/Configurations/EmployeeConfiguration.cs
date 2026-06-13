using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<EmployeeRecord>
{
    public void Configure(EntityTypeBuilder<EmployeeRecord> theBuilder)
    {
        theBuilder.ToTable("Employees");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        theBuilder.Property(x => x.Position).HasMaxLength(100).IsRequired();
        theBuilder.Property(x => x.HireDate).IsRequired();
        theBuilder.Property(x => x.UserId).IsRequired();

        theBuilder.HasQueryFilter(x => x.Status != (int)EntityStatus.Deleted);
    }
}
