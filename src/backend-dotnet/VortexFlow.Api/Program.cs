using Hangfire;
using Microsoft.OpenApi.Models;
using VortexFlow.Api.Authorization;
using VortexFlow.Api.Bootstrap;
using VortexFlow.Api.Extensions;
using VortexFlow.Api.Middleware;
using VortexFlow.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// AddVortexFlowConfiguration() runs the strict startup configuration validator
// (StartupConfigurationValidator) BEFORE any other extension method. It throws
// InvalidOperationException with a consolidated message if any required secret
// is missing, empty, or a known placeholder.
// ---------------------------------------------------------------------------
builder
    .AddVortexFlowConfiguration()
    .AddVortexFlowPersistence()
    .AddVortexFlowAuth()
    .AddVortexFlowRateLimiting()
    .AddVortexFlowCaching()
    .AddVortexFlowMessaging()
    .AddVortexFlowBackgroundJobs()
    .AddVortexFlowSignalR()
    .AddVortexFlowApplicationServices()
    .AddVortexFlowHttpClients()
    .AddVortexFlowCors()
    .AddVortexFlowTelemetry()
    .AddVortexFlowHealthChecks();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
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
        Description = "JWT Authorization header using the Bearer scheme.",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>(),
    });
});

// Middleware classes are activated by the pipeline via app.UseMiddleware<T>(),
// which uses ActivatorUtilities to inject the in-pipeline `RequestDelegate`.
// Registering them with AddTransient<...> forces the default service
// provider to validate a constructor that requires `RequestDelegate`, which
// is not a registered service and cannot be resolved. The Build() call
// then throws "Unable to resolve service for type
// 'Microsoft.AspNetCore.Http.RequestDelegate'".
// The pipeline activation in the block below is the single, correct
// registration point for these types.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseMiddleware<IdempotencyKeyMiddleware>();

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AdminDashboardAuthorizationFilter() },
});

app.MapControllers();
app.MapHub<TrendsHub>("/trendshub");
app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // liveness never depends on downstreams
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

// Bootstrap: migrations, roles, optional admin seed. The seed is opt-in
// (Bootstrap:SeedAdmin) and only safe in Development unless the deployment is
// a controlled one-shot init job.
await DataSeeder.RunAsync(app.Services, app.Configuration, app.Environment);

app.Run();
