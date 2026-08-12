using Serilog;
using Serilog.Events;

namespace SellingNewProduct.API.Observability;

/// <summary>
/// The LOGS pillar (the other two — traces + metrics — live in <see cref="ObservabilitySetup"/>).
/// Routes the standard <see cref="ILogger{T}"/> the whole app already uses through Serilog, writing to
/// BOTH the console and a daily rolling file, with TraceId/SpanId on every line for correlation.
///
/// Behaviour differs by ENVIRONMENT (so it works the same whether local, staging or prod, only the
/// verbosity/paths change):
/// <list type="bullet">
/// <item><b>Development</b> — Debug level, human-readable text template (easy to read while coding).</item>
/// <item><b>Production</b> — Information level, compact JSON lines (a log shipper like Promtail/Filebeat
/// picks the files up into Loki/ELK). No code change — just the environment.</item>
/// </list>
/// File location and retention come from config (<c>FileLogging:*</c>), so a container/prod host can
/// point them at a mounted volume without touching code.
/// </summary>
public static class LoggingSetup
{
    private const string TextTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] TraceId={TraceId} {SourceContext} {Message:lj}{NewLine}{Exception}";

    public static void ConfigureSerilog(this WebApplicationBuilder theBuilder)
    {
        var aEnvironment = theBuilder.Environment;
        var aConfiguration = theBuilder.Configuration;
        var aIsDevelopment = aEnvironment.IsDevelopment();

        var aLogDirectory = aConfiguration["FileLogging:Directory"] ?? "logs";
        var aRetainedFiles = aConfiguration.GetValue<int?>("FileLogging:RetainedFileCountLimit") ?? 14;
        var aFilePath = Path.Combine(aLogDirectory, "log-.txt");

        var aLoggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(aIsDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
            // Framework noise stays at Warning regardless of environment.
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<TraceContextEnricher>()
            .Enrich.WithProperty("Application", "SellingNewProduct.API")
            .Enrich.WithProperty("Environment", aEnvironment.EnvironmentName);

        if (aIsDevelopment)
        {
            // Readable text in dev — console + rolling file.
            aLoggerConfiguration
                .WriteTo.Console(outputTemplate: TextTemplate)
                .WriteTo.File(aFilePath, rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: aRetainedFiles, shared: true, outputTemplate: TextTemplate);
        }
        else
        {
            // Structured JSON in prod — one object per line, ready for a log shipper. Console goes to
            // the container's stdout; the file is for host/volume collection.
            aLoggerConfiguration
                .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
                .WriteTo.File(new Serilog.Formatting.Compact.CompactJsonFormatter(), aFilePath,
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: aRetainedFiles, shared: true);
        }

        // Replace the default logging providers with Serilog; dispose flushes buffered logs on shutdown.
        theBuilder.Logging.ClearProviders();
        theBuilder.Host.UseSerilog(aLoggerConfiguration.CreateLogger(), dispose: true);
    }
}
