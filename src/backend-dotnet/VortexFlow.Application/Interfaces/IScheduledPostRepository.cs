using VortexFlow.Domain.Entities;

namespace VortexFlow.Application.Interfaces;

/// <summary>
/// Repository contract for <see cref="ScheduledPost"/> aggregates. The
/// post is owned by a campaign, so most reads include the campaign
/// navigation eagerly to avoid an N+1 round-trip during ownership
/// disambiguation.
/// </summary>
public interface IScheduledPostRepository
{
    /// <summary>
    /// Loads a post by id (tracked, no navigation included). Used by
    /// background jobs that do not need the parent campaign.
    /// </summary>
    Task<ScheduledPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a post by id with the parent campaign eagerly included
    /// (tracked). Returns null if the post does not exist.
    /// </summary>
    Task<ScheduledPost?> GetByIdWithCampaignAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cheap existence probe (no tracking, no joins). Used by the IDOR
    /// split-404/403 path.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every post whose parent campaign is owned by
    /// <paramref name="ownerId"/>, read-only.
    /// </summary>
    Task<IReadOnlyList<ScheduledPost>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new post for insertion. The actual write happens on
    /// <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    void Add(ScheduledPost post);
}
