using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Audit;
using VortexFlow.Application.DTOs;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Entities;
using VortexFlow.Domain.Enums;
using VortexFlow.Domain.Exceptions;

namespace VortexFlow.Application.Services;

/// <summary>
/// Application service for campaigns and their scheduled posts.
///
/// <para><b>IDOR mitigation strategy.</b></para>
///
/// For every operation that acts on a single, addressable resource
/// (<c>SchedulePostAsync</c>, <c>RetryPostAsync</c>, <c>ReschedulePostAsync</c>)
/// the implementation follows the same two-query pattern:
///
/// <list type="number">
///   <item>Query A: composite filter
///         (<c>Id == X &amp;&amp; (Campaign.)OwnerId == userId</c>). If it
///         returns a row, ownership is confirmed and the operation proceeds.
///   </item>
///   <item>Query B (only when A returns null): existence check by primary key
///         (<c>Id == X</c>). Used purely to disambiguate 404 from 403.
///   </item>
///   <item>If A is null and B is null → <see cref="NotFoundException"/> (404).
///   </item>
///   <item>If A is null and B is non-null → <see cref="ForbiddenException"/>
///         (403) and an audit record is emitted via
///         <see cref="ISecurityAuditLogger.AuthorizationDenied"/>.
///   </item>
/// </list>
///
/// This avoids the classic IDOR pitfall of conflating "the row is not yours"
/// with "the row does not exist", which would let an attacker probe for valid
/// resource ids but not learn which ones are owned by other users.
/// </summary>
public class CampaignService : ICampaignService
{
    private readonly IApplicationDbContext _context;
    private readonly IJobScheduler _jobScheduler;
    private readonly ISecurityAuditLogger _audit;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CampaignService(
        IApplicationDbContext context,
        IJobScheduler jobScheduler,
        ISecurityAuditLogger audit,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _jobScheduler = jobScheduler;
        _audit = audit;
        _httpContextAccessor = httpContextAccessor;
    }

    // -----------------------------------------------------------------------
    // Campaigns
    // -----------------------------------------------------------------------

    public async Task<CampaignDto> CreateCampaignAsync(
        string name, string description, string ownerId, string? tenantId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["ownerId"] = new[] { "ownerId is required" }
            });
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = new[] { "name is required" }
            });
        }

        var campaign = new Campaign
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            OwnerId = ownerId,
            TenantId = tenantId ?? "public",
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync(ct);

        return new CampaignDto
        {
            Id = campaign.Id,
            Name = campaign.Name,
            Description = campaign.Description,
        };
    }

    public async Task<IEnumerable<CampaignDto>> GetCampaignsAsync(
        string ownerId, CancellationToken ct = default)
    {
        return await _context.Campaigns
            .Where(c => c.OwnerId == ownerId)
            .Select(c => new CampaignDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
            })
            .ToListAsync(ct);
    }

    // -----------------------------------------------------------------------
    // Scheduled posts
    // -----------------------------------------------------------------------

    public async Task<ScheduledPostDto> SchedulePostAsync(
        Guid campaignId, string userId, string content, string platform, DateTime scheduledDate,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["userId"] = new[] { "userId is required" }
            });
        }
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["content"] = new[] { "content is required" }
            });
        }
        if (string.IsNullOrWhiteSpace(platform))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["platform"] = new[] { "platform is required" }
            });
        }
        var scheduledUtc = DateTime.SpecifyKind(scheduledDate, DateTimeKind.Utc);
        if (scheduledUtc <= DateTime.UtcNow)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["scheduledDate"] = new[] { "scheduledDate must be in the future (UTC)" }
            });
        }

        // IDOR-safe ownership check on the parent campaign.
        var campaign = await LoadOwnedCampaignOrThrowAsync(campaignId, userId, ct);

        var post = new ScheduledPost
        {
            CampaignId = campaignId,
            Content = content,
            Platform = platform,
            ScheduledDate = scheduledUtc,
            Status = PostStatus.Pending,
            TenantId = campaign.TenantId,
        };

        _context.ScheduledPosts.Add(post);
        await _context.SaveChangesAsync(ct);

        post.HangfireJobId = _jobScheduler.SchedulePublishPostJob(post.Id, scheduledUtc);
        await _context.SaveChangesAsync(ct);

        return MapToDto(post);
    }

    public async Task<ScheduledPostDto> RetryPostAsync(
        Guid postId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["userId"] = new[] { "userId is required" }
            });
        }

        // IDOR-safe ownership check on the post. Throws 404 or 403 as appropriate.
        var post = await LoadOwnedPostOrThrowAsync(postId, userId, ct);

        post.Status = PostStatus.Pending;
        var newDate = post.ScheduledDate > DateTime.UtcNow ? post.ScheduledDate : DateTime.UtcNow.AddMinutes(5);
        post.ScheduledDate = newDate;

        // Cancel any previous job and enqueue a fresh one to avoid duplicate publishes.
        if (!string.IsNullOrEmpty(post.HangfireJobId))
        {
            _jobScheduler.CancelJob(post.HangfireJobId);
        }
        post.HangfireJobId = _jobScheduler.SchedulePublishPostJob(post.Id, newDate);

        await _context.SaveChangesAsync(ct);
        return MapToDto(post);
    }

    public async Task<IEnumerable<ScheduledPostDto>> GetPostsAsync(
        string userId, CancellationToken ct = default)
    {
        return await _context.ScheduledPosts
            .Where(p => p.Campaign != null && p.Campaign.OwnerId == userId)
            .Select(p => new ScheduledPostDto
            {
                Id = p.Id,
                CampaignId = p.CampaignId,
                Content = p.Content,
                Platform = p.Platform,
                ScheduledDate = p.ScheduledDate,
                Status = p.Status.ToString(),
            })
            .ToListAsync(ct);
    }

    public async Task<ScheduledPostDto> ReschedulePostAsync(
        Guid postId, string userId, DateTime newDate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["userId"] = new[] { "userId is required" }
            });
        }

        var newUtc = DateTime.SpecifyKind(newDate, DateTimeKind.Utc);
        if (newUtc <= DateTime.UtcNow)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["date"] = new[] { "date must be in the future (UTC)" }
            });
        }

        // IDOR-safe ownership check on the post. Throws 404 or 403 as appropriate.
        var post = await LoadOwnedPostOrThrowAsync(postId, userId, ct);

        post.ScheduledDate = newUtc;

        // Reschedule: cancel the old Hangfire job and enqueue a new one. Without
        // this the previous job would still fire at the old time.
        if (!string.IsNullOrEmpty(post.HangfireJobId))
        {
            _jobScheduler.CancelJob(post.HangfireJobId);
        }
        post.HangfireJobId = _jobScheduler.SchedulePublishPostJob(post.Id, newUtc);

        await _context.SaveChangesAsync(ct);
        return MapToDto(post);
    }

    // -----------------------------------------------------------------------
    // Ownership-aware loaders
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads a campaign by id and verifies ownership in a single composite
    /// EF query. When the row is missing or not owned, disambiguates between
    /// <see cref="NotFoundException"/> (404) and <see cref="ForbiddenException"/>
    /// (403) so the API does not leak existence information to non-owners
    /// while still telling legitimate callers which case applies.
    /// </summary>
    private async Task<Campaign> LoadOwnedCampaignOrThrowAsync(
        Guid campaignId, string userId, CancellationToken ct)
    {
        // Query A — composite ownership condition (the audit-mandated pattern).
        var owned = await _context.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == campaignId && c.OwnerId == userId, ct);

        if (owned is not null)
        {
            return owned;
        }

        // Query B — existence check, used ONLY to disambiguate 404 vs 403.
        // This single primary-key lookup has negligible cost.
        var exists = await _context.Campaigns
            .AsNoTracking()
            .AnyAsync(c => c.Id == campaignId, ct);

        if (!exists)
        {
            throw new NotFoundException("Campaign", campaignId);
        }

        // The row exists, so the failure of Query A means the caller is not
        // the owner. This is a 403, not a 404.
        _audit.AuthorizationDenied(userId, "Campaign", campaignId.ToString(), _httpContextAccessor.HttpContext?.ClientIp());
        throw new ForbiddenException(
            $"You do not have permission to access campaign '{campaignId}'.");
    }

    /// <summary>
    /// Loads a scheduled post together with its parent campaign and verifies
    /// ownership in a single composite EF query. When the row is missing or
    /// not owned, disambiguates between <see cref="NotFoundException"/> (404)
    /// and <see cref="ForbiddenException"/> (403) for the same reason described
    /// in <see cref="LoadOwnedCampaignOrThrowAsync"/>.
    /// </summary>
    private async Task<ScheduledPost> LoadOwnedPostOrThrowAsync(
        Guid postId, string userId, CancellationToken ct)
    {
        // Query A — composite ownership condition (the audit-mandated pattern).
        // We project to a tuple to avoid tracking a possibly-orphan entity; if A
        // succeeds we re-load with tracking for the mutation phase.
        var ownedProjection = await _context.ScheduledPosts
            .AsNoTracking()
            .Where(p => p.Id == postId
                     && p.Campaign != null
                     && p.Campaign.OwnerId == userId)
            .Select(p => new { p.Id })
            .FirstOrDefaultAsync(ct);

        if (ownedProjection is not null)
        {
            // Re-load with tracking + the navigation eagerly loaded so the
            // caller can mutate and call SaveChangesAsync.
            var tracked = await _context.ScheduledPosts
                .Include(p => p.Campaign)
                .FirstAsync(p => p.Id == postId, ct);
            return tracked;
        }

        // Query B — existence check, used ONLY to disambiguate 404 vs 403.
        var exists = await _context.ScheduledPosts
            .AsNoTracking()
            .AnyAsync(p => p.Id == postId, ct);

        if (!exists)
        {
            throw new NotFoundException("ScheduledPost", postId);
        }

        // The row exists, so the failure of Query A means the caller is not
        // the owner. This is a 403, not a 404.
        _audit.AuthorizationDenied(userId, "ScheduledPost", postId.ToString(), _httpContextAccessor.HttpContext?.ClientIp());
        throw new ForbiddenException(
            $"You do not have permission to access scheduled post '{postId}'.");
    }

    private static ScheduledPostDto MapToDto(ScheduledPost post) => new()
    {
        Id = post.Id,
        CampaignId = post.CampaignId,
        Content = post.Content,
        Platform = post.Platform,
        ScheduledDate = post.ScheduledDate,
        Status = post.Status.ToString(),
    };
}
