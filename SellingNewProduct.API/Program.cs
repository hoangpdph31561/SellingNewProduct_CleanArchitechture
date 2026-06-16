using SellingNewProduct.API;
using SellingNewProduct.API.Middleware;
using SellingNewProduct.Domain;
using SellingNewProduct.Infrastructure.MongoDB;
using SellingNewProduct.Infrastructure.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Each layer registers its own services through a dedicated extension:
builder.Services.AddApiServices();      // presentation: controllers, filter, OpenAPI, validators, hasher
builder.Services.AddDomainServices();   // business logic services (API calls these, never repositories)

// Composition root: pick ONE database implementation. This is the only place
// in the whole solution that knows which database is in use. The domain and
// the controllers are identical regardless of the choice below.
var aProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";

if (string.Equals(aProvider, "MongoDB", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddMongoInfrastructure(builder.Configuration);
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
