using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;
using VortexFlow.Api.Authorization;
using VortexFlow.Api.Bootstrap;
using VortexFlow.Api.Middleware;
using VortexFlow.Application.Audit;
using VortexFlow.Application.Auth;
using VortexFlow.Application.Cache;
using VortexFlow.Application.Interfaces;
using VortexFlow.Application.Services;
using VortexFlow.Application.Tenancy;
using VortexFlow.Domain.Entities;
using VortexFlow.Infrastructure.Auth;
using VortexFlow.Infrastructure.Cache;
using VortexFlow.Infrastructure.Data;
using VortexFlow.Infrastructure.Hubs;
using VortexFlow.Infrastructure.Messaging;
using VortexFlow.Infrastructure.Services;
using VortexFlow.Infrastructure.Tenancy;

namespace VortexFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddVortexFlowConfiguration(this WebApplicationBuilder builder)
    {
        // Fail-fast on missing/invalid required configuration so the container
        // does not boot with half-known secrets. The validator runs BEFORE any
        // DI wiring (AddPersistence, AddAuth, etc.), so we never partially
        // start with an insecure config.
        StartupConfigurationValidator.Validate(builder.Configuration, builder.Environment);
        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowPersistence(this WebApplicationBuilder builder)
    {
        var connStr = builder.Configuration.GetConnectionString("Postgres")!;
        builder.Services.AddDbContext<VortexFlowDbContext>(options =>
            options.UseNpgsql(connStr));
        builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<VortexFlowDbContext>());
        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowAuth(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ITokenService, TokenService>();
        builder.Services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();
        builder.Services.AddScoped<ITenantProvider, ClaimsTenantProvider>();
        builder.Services.AddSingleton<ISecurityAuditLogger, SecurityAuditLogger>();
        builder.Services.AddSingleton<IAuthorizationHandler, RevokedTokenHandler>();

        builder.Services.AddIdentity<User, IdentityRole>(o =>
            {
                o.Password.RequireDigit = true;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequiredLength = 12;
                o.Password.RequireUppercase = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;
                o.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<VortexFlowDbContext>()
            .AddDefaultTokenProviders();

        var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!));
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.SaveToken = true;
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:ValidIssuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:ValidAudience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwtKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async ctx =>
                    {
                        var jti = ctx.Principal?.FindFirst("jti")?.Value;
                        if (string.IsNullOrEmpty(jti)) return;
                        var store = ctx.HttpContext.RequestServices.GetRequiredService<ITokenRevocationStore>();
                        if (await store.IsRevokedAsync(jti, ctx.HttpContext.RequestAborted))
                        {
                            ctx.Fail("Token has been revoked.");
                        }
                    },
                };
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, IngestApiKeyAuthenticationHandler>(
                IngestApiKeyAuthenticationHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy("InternalApiKey", p =>
            {
                p.AuthenticationSchemes = new[] { IngestApiKeyAuthenticationHandler.SchemeName };
                p.RequireAuthenticatedUser();
            });
            // User JWT policy (default) also enforces the revocation store.
            o.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new RevokedTokenRequirement())
                .Build();
        });

        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(o =>
        {
            // Strict for authentication endpoints
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
            // Generous for state-changing endpoints (idempotency keys protect against dupes)
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

    public static WebApplicationBuilder AddVortexFlowCaching(this WebApplicationBuilder builder)
    {
        var redisConnStr = builder.Configuration.GetConnectionString("Redis")!;
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnStr));
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ITrendCache, RedisTrendCache>();
        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<TrendProcessedConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(builder.Configuration.GetConnectionString("RabbitMq"));
                cfg.ReceiveEndpoint("trends_processed_queue", e =>
                {
                    e.ConfigureConsumer<TrendProcessedConsumer>(context);
                    // Dead-letter on repeated failure
                    e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1)));
                });
            });
        });
        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowBackgroundJobs(this WebApplicationBuilder builder)
    {
        var connStr = builder.Configuration.GetConnectionString("Postgres")!;
        builder.Services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(p => p.UseNpgsqlConnection(connStr)));
        builder.Services.AddHangfireServer();
        builder.Services.AddScoped<IJobScheduler, JobScheduler>();
        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowSignalR(this WebApplicationBuilder builder)
    {
        builder.Services.AddSignalR();
        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICampaignService, CampaignService>();
        builder.Services.AddSingleton<VortexFlow.Api.Metrics.AppMetrics>();
        return builder;
    }

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

    public static WebApplicationBuilder AddVortexFlowTelemetry(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = builder.Configuration.GetValue<string>("Otlp:Endpoint") ?? "http://localhost:4317";

        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpEndpoint);
                o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            });
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("VortexFlow.Api", serviceVersion: "1.0.0", serviceInstanceId: Environment.MachineName))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation(o =>
                {
                    o.Filter = req =>
                        !req.Request.Path.StartsWithSegments("/health") &&
                        !req.Request.Path.StartsWithSegments("/swagger") &&
                        !req.Request.Path.StartsWithSegments("/metrics");
                });
                t.AddHttpClientInstrumentation();
                t.AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation();
                m.AddHttpClientInstrumentation();
                m.AddRuntimeInstrumentation();
                m.AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "VortexFlow.Api");
                m.AddPrometheusExporter();
                m.AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
            });
        return builder;
    }

    public static WebApplicationBuilder AddVortexFlowHealthChecks(this WebApplicationBuilder builder)
    {
        var connStr = builder.Configuration.GetConnectionString("Postgres")!;
        var redisConnStr = builder.Configuration.GetConnectionString("Redis")!;
        builder.Services.AddHealthChecks()
            .AddNpgSql(connStr, name: "postgres", tags: new[] { "ready" })
            .AddRedis(redisConnStr, name: "redis", tags: new[] { "ready" });
        return builder;
    }
}
