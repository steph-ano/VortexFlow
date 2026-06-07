using VortexFlow.Domain.Entities;

namespace VortexFlow.Application.Interfaces;

/// <summary>
/// Unit of Work contract. The Application layer depends on this interface
/// for transaction commit and NOTHING ELSE; queries go through repositories
/// (<see cref="ICampaignRepository"/>, <see cref="IScheduledPostRepository"/>,
/// <see cref="ITrendSnapshotRepository"/>). This is what keeps the
/// Application layer free of Entity Framework types.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes accumulated in the underlying stores.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
