using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.RateLimiting;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;
using VortexFlow.Api.Bootstrap;
using VortexFlow.Application.Cache;
using VortexFlow.Application.Interfaces;
using VortexFlow.Application.Services;
using VortexFlow.Infrastructure.Cache;
using VortexFlow.Infrastructure.Services;

namespace VortexFlow.Api.Extensions;

/// <summary>
/// Cross-cutting infrastructure extensions that do not belong to the four
/// main pillars (Persistence, Auth, Messaging, Observability) but are still
/// part of the application composition root. Each method is a small,
/// single-responsibility extension; the four pillars live in their own
/// files (see <c>PersistenceExtensions</c>, <c>AuthExtensions</c>,
/// <c>MessagingExtensions</c>, <c>ObservabilityExtensions</c>).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Runs the strict startup configuration validator BEFORE any other
    /// extension method. Throws <see cref="InvalidOperationException"/>
    /// with a consolidated message if any required secret is missing,
    /// empty, or a known placeholder. The container therefore never boots
    /// with a half-known config.
    /// </summary>
    public static WebApplicationBuilder AddVortexFlowConfiguration(this WebApplicationBuilder builder)
    {
        StartupConfigurationValidator.Validate(builder.Configuration, builder.Environment);
        return builder;
    }

    /// <summary>
    /// Rate limiting for the auth surface (5 req/min/IP) and the
    /// state-changing surface (30 req/min/user-or-IP). Idempotency keys
    /// cover the latter for legitimate retries; the limit blocks brute
    /// force / scraping.
    /// </summary>
    public static WebApplicationBuilder AddVortexFlowRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(o =>
        {
            o.AddPolicy("auth", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? ctx.User.Identity?.Name ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
            o.AddPolicy("write", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
        });
        return builder;
    }

    /// <summary>
    /// Redis cache + in-memory cache. Redis is the shared store; the
    /// in-memory cache is a local hot tier that survives only one process.
    /// </summary>
    public static WebApplicationBuilder AddVortexFlowCaching(this WebApplicationBuilder builder)
    {
        var redisConnStr = builder.Configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnStr));
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ITrendCache, RedisTrendCache>();
        return builder;
    }

    /// <summary>
    /// Hangfire backed by PostgreSQL. The Hangfire schema lives in the same
    /// database as the application data; the dashboard is locked behind
    /// an admin role filter (see <c>AdminDashboardAuthorizationFilter</c>).
    /// </summary>
    public static WebApplicationBuilder AddVortexFlowBackgroundJobs(this WebApplicationBuilder builder)
    {
        var connStr = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        builder.Services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(p => p.UseNpgsqlConnection(connStr)));
        builder.Services.AddHangfireServer();
        builder.Services.AddScoped<IJobScheduler, JobScheduler>();
        return builder;
    }

    /// <summary>
    /// SignalR for the realtime trends hub.
    /// </summary>
    public static WebApplicationBuilder AddVortexFlowSignalR(this WebApplicationBuilder builder)
    {
        builder.Services.AddSignalR();
        return builder;
    }

    /// <summary>
    /// Application-layer services and the metrics facade.
    /// </summary>
    public static WebApplicationBuilder AddVortexFlowApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICampaignService, CampaignService>();
        builder.Services.AddSingleton<VortexFlow.Api.Metrics.AppMetrics>();
        return builder;
    }

    /// <summary>
    /// Typed HTTP clients with Polly retry policies.
    /// </summary>
    public static WebApplicationBuilder AddVortexFlowHttpClients(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("PythonServiceClient", c =>
        {
            c.BaseAddress = new Uri("http://localhost:8000/");
            c.Timeout = TimeSpan.FromSeconds(10);
        }).AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        return builder;
    }

    /// <summary>
    /// CORS for the Vite dev server (5173). Origins are pinned so the
    /// browser preflight can succeed; the policy is <c>AllowCredentials</c>
    /// because the refresh-token cookie must travel.
    /// </summary>
    public static WebApplicationBuilder AddVortexFlowCors(this WebApplicationBuilder builder)
    {
        builder.Services.AddCors(o =>
        {
            o.AddPolicy("AllowFrontend", p => p
                .WithOrigins("http://localhost:5173", "https://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });
        return builder;
    }
}
