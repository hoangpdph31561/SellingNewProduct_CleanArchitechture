using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SellingNewProduct.Domain.Common;
using SellingNewProduct.Infrastructure.SqlServer.Models;

namespace SellingNewProduct.Infrastructure.SqlServer.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> theBuilder)
    {
        theBuilder.ToTable("Users");
        theBuilder.HasKey(x => x.Id);

        theBuilder.Property(x => x.Username).HasMaxLength(100).IsRequired();
        theBuilder.HasIndex(x => x.Username).IsUnique();

        theBuilder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        theBuilder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        theBuilder.Property(x => x.Role).IsRequired();
        theBuilder.Property(x => x.Status).IsRequired();

        // Soft delete: never return rows flagged as Deleted.
        theBuilder.HasQueryFilter(x => x.Status != (int)EntityStatus.Deleted);
    }
}
