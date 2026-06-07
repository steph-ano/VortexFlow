using VortexFlow.Domain.Entities;

namespace VortexFlow.Application.Interfaces;

/// <summary>
/// Repository contract for <see cref="TrendSnapshot"/> writes and reads.
/// TrendSnapshots are append-mostly: each scrape produces a new row keyed
/// on (Platform, CapturedAt), and the public read endpoint pulls the
/// most recent ones ordered by CapturedAt.
/// </summary>
public interface ITrendSnapshotRepository
{
    /// <summary>
    /// Looks up a snapshot by its deterministic EventId. Returns null if
    /// no row exists. Used by the idempotent consumer to decide between
    /// insert and skip.
    /// </summary>
    Task<TrendSnapshot?> FindByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new snapshot for insertion.
    /// </summary>
    void Add(TrendSnapshot snapshot);

    /// <summary>
    /// Returns the <paramref name="limit"/> most recent snapshots ordered
    /// by <see cref="TrendSnapshot.CapturedAt"/> descending.
    /// </summary>
    Task<IReadOnlyList<TrendSnapshot>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent snapshot for a given platform/hashtag pair
    /// (the tag must be present in the snapshot's Hashtags array). Used by
    /// the cache fallback path when Redis is unavailable.
    /// </summary>
    Task<TrendSnapshot?> FindLatestByPlatformAndHashtagAsync(
        string platform,
        string hashtag,
        CancellationToken cancellationToken = default);
}
