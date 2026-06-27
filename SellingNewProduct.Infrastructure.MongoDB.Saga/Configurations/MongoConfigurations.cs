using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MongoDB.EntityFrameworkCore.Extensions;
using SellingNewProduct.Infrastructure.MongoDB.Saga.Models;

namespace SellingNewProduct.Infrastructure.MongoDB.Saga.Configurations;

// Each aggregate maps to its own collection. In the saga provider MongoDB owns only the
// catalogue + people aggregates (Order/Payment live in SQL). The provider has no migrations and
// no global query filters, so soft delete is filtered inside the repositories instead.

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

internal sealed class SagaInstanceConfiguration : IEntityTypeConfiguration<SagaInstanceDocument>
{
    public void Configure(EntityTypeBuilder<SagaInstanceDocument> theBuilder)
    {
        theBuilder.ToCollection("saga_instances");
        theBuilder.HasKey(x => x.Id);
        theBuilder.OwnsMany(x => x.Steps);
    }
}
