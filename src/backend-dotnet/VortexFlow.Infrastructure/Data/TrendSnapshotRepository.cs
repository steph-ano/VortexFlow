using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Entities;

namespace VortexFlow.Infrastructure.Data;

/// <summary>
/// EF Core-backed <see cref="ITrendSnapshotRepository"/>. Snapshots are
/// append-mostly; reads always use <c>AsNoTracking</c> because the data
/// is immutable once written and the consumer only serialises it.
/// </summary>
public sealed class TrendSnapshotRepository : ITrendSnapshotRepository
{
    private readonly VortexFlowDbContext _db;

    public TrendSnapshotRepository(VortexFlowDbContext db) => _db = db;

    public Task<TrendSnapshot?> FindByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
        => _db.TrendSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == eventId, cancellationToken);

    public void Add(TrendSnapshot snapshot) => _db.TrendSnapshots.Add(snapshot);

    public async Task<IReadOnlyList<TrendSnapshot>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => await _db.TrendSnapshots
            .AsNoTracking()
            .OrderByDescending(s => s.CapturedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<TrendSnapshot?> FindLatestByPlatformAndHashtagAsync(
        string platform,
        string hashtag,
        CancellationToken cancellationToken = default)
        => _db.TrendSnapshots
            .AsNoTracking()
            .Where(s => s.Platform == platform && s.Hashtags.Contains(hashtag))
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
