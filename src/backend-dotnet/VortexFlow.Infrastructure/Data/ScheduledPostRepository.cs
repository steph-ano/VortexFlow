using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Entities;

namespace VortexFlow.Infrastructure.Data;

/// <summary>
/// EF Core-backed <see cref="IScheduledPostRepository"/>. Read paths
/// eager-load the parent <c>Campaign</c> navigation so the
/// Application layer can validate ownership in a single round-trip
/// (see <c>CampaignService.LoadOwnedPostOrThrowAsync</c>).
/// </summary>
public sealed class ScheduledPostRepository : IScheduledPostRepository
{
    private readonly VortexFlowDbContext _db;

    public ScheduledPostRepository(VortexFlowDbContext db) => _db = db;

    public Task<ScheduledPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.ScheduledPosts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<ScheduledPost?> GetByIdWithCampaignAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.ScheduledPosts
            .Include(p => p.Campaign)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.ScheduledPosts.AsNoTracking().AnyAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ScheduledPost>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
        => await _db.ScheduledPosts
            .AsNoTracking()
            .Where(p => p.Campaign != null && p.Campaign.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

    public void Add(ScheduledPost post) => _db.ScheduledPosts.Add(post);
}
