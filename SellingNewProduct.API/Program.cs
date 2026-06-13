using FluentValidation;
using SellingNewProduct.API.Middleware;
using SellingNewProduct.API.Validators;
using SellingNewProduct.Infrastructure.MongoDB;
using SellingNewProduct.Infrastructure.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// API-level validators (request shape).
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();

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
