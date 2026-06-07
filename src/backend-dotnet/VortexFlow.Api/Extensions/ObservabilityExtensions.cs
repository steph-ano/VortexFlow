using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace VortexFlow.Api.Extensions;

/// <summary>
/// Observability-layer bootstrap: structured logging via OpenTelemetry +
/// OTLP, distributed tracing, Prometheus metrics scraping, and health
/// checks for liveness/readiness probes.
///
/// All telemetry is shipped to a single OTLP endpoint (configurable via
/// <c>Otlp:Endpoint</c>); the Prometheus exporter additionally exposes
/// <c>/metrics</c> for the in-cluster scraper.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Path segments excluded from the trace exporter to keep cardinality
    /// bounded and to avoid sampling the probe endpoints (which fire every
    /// few seconds and would drown out business traces).
    /// </summary>
    private static readonly string[] ExcludedTracePaths =
    {
        "/health", "/swagger", "/metrics"
    };

    public static WebApplicationBuilder AddVortexFlowTelemetry(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = builder.Configuration.GetValue<string>("Otlp:Endpoint")
            ?? "http://localhost:4317";

        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpEndpoint);
                o.Protocol = OtlpExportProtocol.Grpc;
            });
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: "VortexFlow.Api",
                serviceVersion: "1.0.0",
                serviceInstanceId: Environment.MachineName))
            .WithTracing(t => ConfigureTracing(t, otlpEndpoint))
            .WithMetrics(m => ConfigureMetrics(m, otlpEndpoint));

        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowHealthChecks(this WebApplicationBuilder builder)
    {
        var connStr = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
        var redisConnStr = builder.Configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

        builder.Services.AddHealthChecks()
            .AddNpgSql(connStr, name: "postgres", tags: new[] { "ready" })
            .AddRedis(redisConnStr, name: "redis", tags: new[] { "ready" });

        return builder;
    }

    private static void ConfigureTracing(TracerProviderBuilder t, string otlpEndpoint)
    {
        t.AddAspNetCoreInstrumentation(o =>
        {
            o.Filter = ctx =>
            {
                var path = ctx.Request.Path;
                foreach (var excluded in ExcludedTracePaths)
                {
                    if (path.StartsWithSegments(excluded)) return false;
                }
                return true;
            };
        });
        t.AddHttpClientInstrumentation();
        t.AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(otlpEndpoint);
            o.Protocol = OtlpExportProtocol.Grpc;
        });
    }

    private static void ConfigureMetrics(MeterProviderBuilder m, string otlpEndpoint)
    {
        m.AddAspNetCoreInstrumentation();
        m.AddHttpClientInstrumentation();
        m.AddRuntimeInstrumentation();
        m.AddMeter(
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "VortexFlow.Api");
        m.AddPrometheusExporter();
        m.AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(otlpEndpoint);
            o.Protocol = OtlpExportProtocol.Grpc;
        });
    }
}
