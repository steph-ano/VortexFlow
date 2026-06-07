using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Interfaces;
using VortexFlow.Infrastructure.Cache;
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

        // The Application layer talks to the database exclusively through
        // repositories and IUnitOfWork. DbSet<T> on VortexFlowDbContext is
        // `internal`, which is what forces every consumer to go through the
        // abstractions registered here.
        builder.Services.AddScoped<IUnitOfWork>(sp =>
            sp.GetRequiredService<VortexFlowDbContext>());

        builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
        builder.Services.AddScoped<IScheduledPostRepository, ScheduledPostRepository>();
        builder.Services.AddScoped<ITrendSnapshotRepository, TrendSnapshotRepository>();

        return builder;
    }
}
