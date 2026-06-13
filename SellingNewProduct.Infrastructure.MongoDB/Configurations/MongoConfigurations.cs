using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.EntityFrameworkCore.Extensions;
using SellingNewProduct.Infrastructure.MongoDB.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Configurations;

// Each aggregate maps to its own collection. The MongoDB provider has no
// migrations and no global query filters, so soft delete is filtered inside
// the repositories instead. Keys named Id are mapped to the document's _id.

internal sealed class UserConfiguration : IEntityTypeConfiguration<UserDocument>
{
    public void Configure(EntityTypeBuilder<UserDocument> theBuilder)
    {
        theBuilder.ToCollection("users");
        theBuilder.HasKey(x => x.Id);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerDocument>
{
    public void Configure(EntityTypeBuilder<CustomerDocument> theBuilder)
    {
        theBuilder.ToCollection("customers");
        theBuilder.HasKey(x => x.Id);
    }
}

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> theBuilder)
    {
        theBuilder.ToCollection("employees");
        theBuilder.HasKey(x => x.Id);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<CategoryDocument>
{
    public void Configure(EntityTypeBuilder<CategoryDocument> theBuilder)
    {
        theBuilder.ToCollection("categories");
        theBuilder.HasKey(x => x.Id);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<ProductDocument>
{
    public void Configure(EntityTypeBuilder<ProductDocument> theBuilder)
    {
        theBuilder.ToCollection("products");
        theBuilder.HasKey(x => x.Id);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<OrderDocument>
{
    public void Configure(EntityTypeBuilder<OrderDocument> theBuilder)
    {
        theBuilder.ToCollection("orders");
        theBuilder.HasKey(x => x.Id);

        // Details are embedded inside the order document (nested array).
        theBuilder.OwnsMany(x => x.Details);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentDocument>
{
    public void Configure(EntityTypeBuilder<PaymentDocument> theBuilder)
    {
        theBuilder.ToCollection("payments");
        theBuilder.HasKey(x => x.Id);
    }
}
