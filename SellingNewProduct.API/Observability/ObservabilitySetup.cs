using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SellingNewProduct.Application.Common.Telemetry;
using SellingNewProduct.Infrastructure.Messaging.Outbox;
using SellingNewProduct.Infrastructure.SqlServer.Saga.Persistence;

namespace SellingNewProduct.API.Observability;

/// <summary>
/// Wires the three observability pillars for the API:
/// <list type="bullet">
/// <item><b>Traces</b> — auto-instruments incoming HTTP (ASP.NET Core), outgoing HTTP (HttpClient) and
/// EF Core queries, plus the custom <see cref="AppTelemetry"/> span per MediatR request. This is
/// distributed tracing: one TraceId threads a request through the controller, the handler, and the DB.</item>
/// <item><b>Metrics</b> — runtime (GC, thread pool), ASP.NET Core request metrics, and the custom app +
/// outbox meters, exposed for Prometheus to scrape at <c>/metrics</c>.</item>
/// <item><b>Health checks</b> — a liveness probe (is the process up?) and a readiness probe (can it serve
/// traffic? — includes the SQL DbContext), for load balancers / Kubernetes.</item>
/// </list>
/// This is the only file that references the OpenTelemetry SDK; the layers being observed stay on pure
/// BCL primitives, so nothing below the API is coupled to a telemetry vendor.
/// </summary>
public static class ObservabilitySetup
{
    public static IServiceCollection AddObservability(
        this IServiceCollection theServices, IConfiguration theConfiguration, IWebHostEnvironment theEnvironment)
    {
        var aServiceName = theConfiguration["OpenTelemetry:ServiceName"] ?? "SellingNewProduct.API";

        // Where to SHIP telemetry differs by environment — and nothing is hardcoded to localhost:
        //   - Dev: OtlpEndpoint empty -> print traces to the console; scrape metrics at /metrics.
        //   - Prod: set OpenTelemetry:OtlpEndpoint (e.g. an OTel Collector at http://otel-collector:4317,
        //           overridable by the env var OpenTelemetry__OtlpEndpoint) -> push traces + metrics there.
        // /metrics stays on in every environment so Prometheus can scrape regardless.
        var aOtlpEndpoint = theConfiguration["OpenTelemetry:OtlpEndpoint"];
        var aHasOtlp = !string.IsNullOrWhiteSpace(aOtlpEndpoint);

        theServices.AddOpenTelemetry()
            .ConfigureResource(theResource => theResource
                .AddService(aServiceName, serviceVersion: "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = theEnvironment.EnvironmentName,
                    ["host.name"] = Environment.MachineName,
                }))
            .WithTracing(theTracing =>
            {
                theTracing
                    .AddAspNetCoreInstrumentation(theOptions =>
                    {
                        theOptions.RecordException = true;
                        // Don't trace the probes/scrape themselves — pure noise.
                        theOptions.Filter = theContext =>
                            !theContext.Request.Path.StartsWithSegments("/health") &&
                            !theContext.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddSource(AppTelemetry.ActivitySourceName);

                if (theEnvironment.IsDevelopment())
                {
                    theTracing.AddConsoleExporter();
                }

                if (aHasOtlp)
                {
                    theTracing.AddOtlpExporter(theOptions => theOptions.Endpoint = new Uri(aOtlpEndpoint!));
                }
            })
            .WithMetrics(theMetrics =>
            {
                theMetrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(AppTelemetry.MeterName)
                    .AddMeter(OutboxMetrics.MeterName)
                    .AddPrometheusExporter(); // always: exposes /metrics for Prometheus to scrape

                if (aHasOtlp)
                {
                    theMetrics.AddOtlpExporter(theOptions => theOptions.Endpoint = new Uri(aOtlpEndpoint!));
                }
            });

        theServices.AddHealthChecks()
            // Readiness: the SQL business store must be reachable before we accept traffic.
            .AddDbContextCheck<AppDbContext>("sql-store", tags: ["ready"]);

        return theServices;
    }
}
