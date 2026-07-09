using SellingNewProduct.API;
using SellingNewProduct.API.Middleware;
using SellingNewProduct.Application;
using SellingNewProduct.Infrastructure.MongoDB;
using SellingNewProduct.Infrastructure.SqlServer;
using SellingNewProduct.Infrastructure.Saga.Core;
using SellingNewProduct.Infrastructure.SqlServer.Saga;
using SellingNewProduct.Infrastructure.MongoDB.Saga;
using SellingNewProduct.Infrastructure.Messaging;
using SellingNewProduct.Infrastructure.Payments;

var builder = WebApplication.CreateBuilder(args);

// Each layer registers its own services through a dedicated extension:
builder.Services.AddApiServices();          // presentation: controllers, result filter, OpenAPI, hasher
builder.Services.AddApiAuthentication(builder.Configuration); // JWT bearer auth + role authorization + token generator
builder.Services.AddApplicationServices();  // CQRS handlers + MediatR + validation pipeline (API sends via ISender)

// Composition root: pick ONE database implementation. This is the only place
// in the whole solution that knows which database is in use. The domain and
// the controllers are identical regardless of the choice below.
//var aProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
// Polyglot persistence: Order/Payment in SQL Server, the rest in MongoDB, tied together by a
// saga (cross-database transaction via local commits + compensations). Needs BOTH the SqlServer
// and MongoDB connection strings. See docs/saga-hybrid.md.
builder.Services.AddSagaCore();
builder.Services.AddSqlServerSagaInfrastructure(builder.Configuration);
builder.Services.AddMongoSagaInfrastructure(builder.Configuration);

// Event-driven side effects: the SQL side writes domain events to a transactional outbox; this
// relays them onto Kafka (the event backbone), where process managers turn facts into RabbitMQ
// work commands (send email, issue invoice, notify) and an analytics projection reads the same
// log independently. See docs/outbox-kafka-rabbitmq.md.
builder.Services.AddMessaging(builder.Configuration);

// Online payments: the VNPay gateway (build signed pay URL + verify the return/IPN signature).
builder.Services.AddVnPayPayments(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Authentication (who are you? — validate the bearer token) must run before authorization
// (are you allowed? — enforce [Authorize]/roles).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
