using SellingNewProduct.API;
using SellingNewProduct.API.Middleware;
using SellingNewProduct.Application;
using SellingNewProduct.Infrastructure.MongoDB;
using SellingNewProduct.Infrastructure.SqlServer;
using SellingNewProduct.Infrastructure.Saga.Core;
using SellingNewProduct.Infrastructure.SqlServer.Saga;
using SellingNewProduct.Infrastructure.MongoDB.Saga;

var builder = WebApplication.CreateBuilder(args);

// Each layer registers its own services through a dedicated extension:
builder.Services.AddApiServices();          // presentation: controllers, result filter, OpenAPI, hasher
builder.Services.AddApplicationServices();  // CQRS handlers + MediatR + validation pipeline (API sends via ISender)

// Composition root: pick ONE database implementation. This is the only place
// in the whole solution that knows which database is in use. The domain and
// the controllers are identical regardless of the choice below.
var aProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";

if (string.Equals(aProvider, "MongoDB", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddMongoInfrastructure(builder.Configuration);
}
else if (string.Equals(aProvider, "Hybrid", StringComparison.OrdinalIgnoreCase)
         || string.Equals(aProvider, "Saga", StringComparison.OrdinalIgnoreCase))
{
    // Polyglot persistence: Order/Payment in SQL Server, the rest in MongoDB, tied together by a
    // saga (cross-database transaction via local commits + compensations). Needs BOTH the SqlServer
    // and MongoDB connection strings. See docs/saga-hybrid.md.
    builder.Services.AddSagaCore();
    builder.Services.AddSqlServerSagaInfrastructure(builder.Configuration);
    builder.Services.AddMongoSagaInfrastructure(builder.Configuration);
}
else
{
    builder.Services.AddSqlServerInfrastructure(builder.Configuration);
}

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
