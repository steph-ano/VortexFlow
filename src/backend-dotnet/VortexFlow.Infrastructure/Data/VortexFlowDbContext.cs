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
        });
    }
}
