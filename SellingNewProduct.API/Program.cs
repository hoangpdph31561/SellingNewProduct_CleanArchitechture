using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using SellingNewProduct.API;
using SellingNewProduct.API.Middleware;
using SellingNewProduct.API.Observability;
using Serilog;
using SellingNewProduct.Application;
using SellingNewProduct.Infrastructure.MongoDB;
using SellingNewProduct.Infrastructure.SqlServer;
using SellingNewProduct.Infrastructure.Saga.Core;
using SellingNewProduct.Infrastructure.SqlServer.Saga;
using SellingNewProduct.Infrastructure.MongoDB.Saga;
using SellingNewProduct.Infrastructure.Messaging;
using SellingNewProduct.Infrastructure.Payments;

var builder = WebApplication.CreateBuilder(args);

// LOGS pillar: route ILogger through Serilog -> console + rolling file, with TraceId/SpanId on every
// line. Dev = readable text (Debug); Prod = JSON (Information). See docs/observability.md.
builder.ConfigureSerilog();

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

// Observability: OpenTelemetry traces + metrics (Prometheus scrape) + health checks. See
// docs/observability.md. Only the API references the OTel SDK; inner layers stay on BCL primitives.
builder.Services.AddObservability(builder.Configuration, builder.Environment);

var app = builder.Build();

// One structured summary log per HTTP request (method, path, status, elapsed) — carries the TraceId.
app.UseSerilogRequestLogging();

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

// Prometheus scrapes metrics here (runtime + ASP.NET Core + app + outbox meters).
app.MapPrometheusScrapingEndpoint(); // GET /metrics

// Liveness: is the process up at all (no dependency checks — never fail on a slow DB).
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: can we serve traffic? (checks dependencies tagged "ready", e.g. the SQL store).
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = theCheck => theCheck.Tags.Contains("ready")
});

app.Run();
