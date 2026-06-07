using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Entities;

namespace VortexFlow.Infrastructure.Data;

/// <summary>
/// EF Core-backed <see cref="ICampaignRepository"/>. The methods are
/// thin facades over <see cref="VortexFlowDbContext"/>: queries are
/// <c>AsNoTracking</c> by default (the Application layer opts in to
/// tracking explicitly when it needs to mutate), and writes go through
/// <c>Add</c> so the actual <c>INSERT</c> happens on
/// <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public sealed class CampaignRepository : ICampaignRepository
{
    private readonly VortexFlowDbContext _db;

    public CampaignRepository(VortexFlowDbContext db) => _db = db;

    public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Campaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Campaign>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
        => await _db.Campaigns
            .AsNoTracking()
            .Where(c => c.OwnerId == ownerId)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Campaigns.AsNoTracking().AnyAsync(c => c.Id == id, cancellationToken);

    public void Add(Campaign campaign) => _db.Campaigns.Add(campaign);
}
