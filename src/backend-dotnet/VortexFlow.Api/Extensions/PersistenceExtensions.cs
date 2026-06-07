using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Interfaces;
using VortexFlow.Infrastructure.Data;

namespace VortexFlow.Api.Extensions;

/// <summary>
/// Persistence-layer bootstrap: EF Core, the application's relational store,
/// and the abstraction surface that the Application layer depends on.
///
/// This extension is intentionally tiny because the bulk of persistence
/// configuration lives in the Infrastructure project (entity configurations,
/// repositories, migrations). Anything that needs the relational store
/// (Identity, Hangfire, HealthChecks) is wired here as a dependency on the
/// shared <see cref="VortexFlowDbContext"/>.
/// </summary>
public static class PersistenceExtensions
{
    public static WebApplicationBuilder AddVortexFlowPersistence(this WebApplicationBuilder builder)
    {
        var connStr = builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        builder.Services.AddDbContext<VortexFlowDbContext>(options =>
            options.UseNpgsql(connStr));

        // Expose the EF context behind the Application-layer abstraction so
        // services depend on IApplicationDbContext (testable) instead of the
        // concrete DbContext (couples to EF Core).
        builder.Services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<VortexFlowDbContext>());

        return builder;
    }
}
