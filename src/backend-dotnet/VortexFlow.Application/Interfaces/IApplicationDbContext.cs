using Microsoft.EntityFrameworkCore;
using VortexFlow.Domain.Entities;

namespace VortexFlow.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Campaign> Campaigns { get; }
    DbSet<ScheduledPost> ScheduledPosts { get; }
    DbSet<TrendSnapshot> TrendSnapshots { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
