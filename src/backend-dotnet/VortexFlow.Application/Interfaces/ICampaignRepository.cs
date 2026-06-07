using VortexFlow.Domain.Entities;

namespace VortexFlow.Application.Interfaces;

/// <summary>
/// Repository contract for <see cref="Campaign"/> aggregates. Methods are
/// named after use cases, not after LINQ operators, so the Application
/// layer does not need to know about <c>IQueryable</c>, <c>AsNoTracking</c>,
/// or any other EF Core concern.
/// </summary>
public interface ICampaignRepository
{
    /// <summary>
    /// Loads a campaign by id, tracking the entity for mutation.
    /// Used by write paths (<c>SchedulePostAsync</c> schedules a child
    /// post under the loaded parent).
    /// </summary>
    Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every campaign owned by <paramref name="ownerId"/>, read-only.
    /// </summary>
    Task<IReadOnlyList<Campaign>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when a campaign with the given id exists, regardless of
    /// ownership. Used by the IDOR split-404/403 path to disambiguate
    /// "does not exist" from "exists but is not yours" without loading the
    /// whole aggregate.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new campaign for insertion. The actual write happens on
    /// <see cref="IUnitOfWork.SaveChangesAsync"/>.
    /// </summary>
    void Add(Campaign campaign);
}
