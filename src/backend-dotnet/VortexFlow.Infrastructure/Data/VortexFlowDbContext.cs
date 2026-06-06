using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Entities;

namespace VortexFlow.Infrastructure.Data;

public class VortexFlowDbContext : IdentityDbContext<User>, IApplicationDbContext
{
    public VortexFlowDbContext(DbContextOptions<VortexFlowDbContext> options) : base(options)
    {
    }

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<ScheduledPost> ScheduledPosts => Set<ScheduledPost>();
    public DbSet<TrendSnapshot> TrendSnapshots => Set<TrendSnapshot>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TrendSnapshot>(entity =>
        {
            // Configure Metrics column as JSONB
            entity.Property(e => e.Metrics)
                  .HasColumnType("jsonb");

            // gin index natively supported
            entity.HasIndex(e => e.Metrics)
                  .HasMethod("gin");

            // Composite (Platform, CapturedAt) index for the time-series query path
            entity.HasIndex(e => new { e.Platform, e.CapturedAt });
        });

        // Tenant column is reserved for future multi-tenant enforcement (ADR-0002).
        // Ownership is enforced at the service layer (CampaignService) to keep the
        // global query filter simple and explicit per call.
        builder.Entity<Campaign>(b =>
        {
            b.HasIndex(c => c.OwnerId);
            b.Property(c => c.TenantId).HasMaxLength(64);
        });
        builder.Entity<ScheduledPost>(b =>
        {
            b.HasIndex(p => p.CampaignId);
            b.Property(p => p.TenantId).HasMaxLength(64);
        });
    }
}
