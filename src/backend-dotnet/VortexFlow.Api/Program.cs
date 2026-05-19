using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VortexFlow.Domain.Entities;
using VortexFlow.Infrastructure.Data;
using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using VortexFlow.Application.Interfaces;
using VortexFlow.Application.Services;
using VortexFlow.Infrastructure.Cache;
using VortexFlow.Infrastructure.Hubs;
using VortexFlow.Infrastructure.Messaging;
using VortexFlow.Infrastructure.Services;
using VortexFlow.Infrastructure.Vault;
using StackExchange.Redis;
using VortexFlow.Application.Cache;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Polly;
using Polly.Extensions.Http;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry
var otlpEndpoint = builder.Configuration.GetValue<string>("Otlp:Endpoint") ?? "http://localhost:4317";

builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);
        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("VortexFlow.Api", serviceVersion: "1.0.0", serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = (req) =>
                !req.Request.Path.StartsWithSegments("/health") &&
                !req.Request.Path.StartsWithSegments("/swagger") &&
                !req.Request.Path.StartsWithSegments("/metrics");
        });
        tracing.AddHttpClientInstrumentation();
        tracing.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        });
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "VortexFlow.Api");
        metrics.AddPrometheusExporter();
        metrics.AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        });
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VortexFlow API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IVaultSecretProvider, EnvironmentVaultSecretProvider>();

var postgresConnStr = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddDbContext<VortexFlowDbContext>(options =>
    options.UseNpgsql(postgresConnStr));
builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<VortexFlowDbContext>());

builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<VortexFlowDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = true; // Secured
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:ValidAudience"],
        ValidIssuer = builder.Configuration["Jwt:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
    };
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });
});

var redisConnStr = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnStr!));
builder.Services.AddSingleton<ITrendCache, RedisTrendCache>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TrendProcessedConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMq"));
        
        cfg.ReceiveEndpoint("trends_processed_queue", e =>
        {
            e.ConfigureConsumer<TrendProcessedConsumer>(context);
        });
    });
});

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(postgresConnStr)));

builder.Services.AddHangfireServer();

builder.Services.AddSignalR();

builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IJobScheduler, JobScheduler>();
builder.Services.AddSingleton<VortexFlow.Api.Metrics.AppMetrics>();

// HttpClient con Polly para Python
builder.Services.AddHttpClient("PythonServiceClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:8000/");
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            builder.WithOrigins("http://localhost:5173") // Strict CORS
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
});

app.MapControllers();
app.MapHub<TrendsHub>("/trendshub");
app.MapPrometheusScrapingEndpoint();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<VortexFlowDbContext>();
    context.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    if (!roleManager.RoleExistsAsync("Admin").Result)
        roleManager.CreateAsync(new IdentityRole("Admin")).Wait();
    if (!roleManager.RoleExistsAsync("Viewer").Result)
        roleManager.CreateAsync(new IdentityRole("Viewer")).Wait();

    if (userManager.FindByEmailAsync("admin@vortexflow.local").Result == null)
    {
        var adminUser = new User
        {
            UserName = "admin@vortexflow.local",
            Email = "admin@vortexflow.local",
            Name = "System Admin"
        };
        var result = userManager.CreateAsync(adminUser, "Admin123!").Result;
        if (result.Succeeded)
        {
            userManager.AddToRoleAsync(adminUser, "Admin").Wait();
        }
    }
}

app.Run();
