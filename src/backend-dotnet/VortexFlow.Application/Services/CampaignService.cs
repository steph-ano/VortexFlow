using Microsoft.AspNetCore.Http;
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
///   <item>Query A via the repository: composite filter
///         (<c>Id == X &amp;&amp; (Campaign.)OwnerId == userId</c>). If it
///         returns a row, ownership is confirmed and the operation proceeds.
///   </item>
///   <item>Query B (only when A returns null): existence check by primary
///         key via <c>ExistsAsync</c>. Used purely to disambiguate 404 from
///         403 without re-loading the aggregate.
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
/// with "the row does not exist", which would let an attacker probe for
/// valid resource ids but not learn which ones are owned by other users.
/// </summary>
public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _campaigns;
    private readonly IScheduledPostRepository _posts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJobScheduler _jobScheduler;
    private readonly ISecurityAuditLogger _audit;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CampaignService(
        ICampaignRepository campaigns,
        IScheduledPostRepository posts,
        IUnitOfWork unitOfWork,
        IJobScheduler jobScheduler,
        ISecurityAuditLogger audit,
        IHttpContextAccessor httpContextAccessor)
    {
        _campaigns = campaigns;
        _posts = posts;
        _unitOfWork = unitOfWork;
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

        _campaigns.Add(campaign);
        await _unitOfWork.SaveChangesAsync(ct);

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
        var campaigns = await _campaigns.GetByOwnerAsync(ownerId, ct);
        return campaigns.Select(c => new CampaignDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
        });
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

        _posts.Add(post);
        await _unitOfWork.SaveChangesAsync(ct);

        post.HangfireJobId = _jobScheduler.SchedulePublishPostJob(post.Id, scheduledUtc);
        await _unitOfWork.SaveChangesAsync(ct);

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

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(post);
    }

    public async Task<IEnumerable<ScheduledPostDto>> GetPostsAsync(
        string userId, CancellationToken ct = default)
    {
        var posts = await _posts.GetByOwnerAsync(userId, ct);
        return posts.Select(p => new ScheduledPostDto
        {
            Id = p.Id,
            CampaignId = p.CampaignId,
            Content = p.Content,
            Platform = p.Platform,
            ScheduledDate = p.ScheduledDate,
            Status = p.Status.ToString(),
        });
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

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(post);
    }

    // -----------------------------------------------------------------------
    // Ownership-aware loaders
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads a campaign by id and verifies ownership in a single composite
    /// query. When the row is missing or not owned, disambiguates between
    /// <see cref="NotFoundException"/> (404) and <see cref="ForbiddenException"/>
    /// (403) so the API does not leak existence information to non-owners
    /// while still telling legitimate callers which case applies.
    /// </summary>
    private async Task<Campaign> LoadOwnedCampaignOrThrowAsync(
        Guid campaignId, string userId, CancellationToken ct)
    {
        // Query A — composite ownership condition (the audit-mandated pattern).
        // The repository's GetByIdAsync returns the tracked aggregate; we
        // verify ownership at the call site because that is business
        // policy, not a storage concern (ownership could one day come
        // from a role/ACL service rather than a column on the row).
        var owned = await _campaigns.GetByIdAsync(campaignId, ct);
        if (owned is not null && owned.OwnerId == userId)
        {
            return owned;
        }

        // Query B — existence check, used ONLY to disambiguate 404 vs 403.
        if (!await _campaigns.ExistsAsync(campaignId, ct))
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
    /// and <see cref="ForbiddenException"/> (403) for the same reason
    /// described in <see cref="LoadOwnedCampaignOrThrowAsync"/>.
    /// </summary>
    private async Task<ScheduledPost> LoadOwnedPostOrThrowAsync(
        Guid postId, string userId, CancellationToken ct)
    {
        // The repository's GetByIdWithCampaignAsync does the include in
        // one round-trip, so ownership can be checked without a follow-up
        // query.
        var post = await _posts.GetByIdWithCampaignAsync(postId, ct);
        if (post is not null && post.Campaign is not null && post.Campaign.OwnerId == userId)
        {
            return post;
        }

        // Query B — existence check, used ONLY to disambiguate 404 vs 403.
        if (!await _posts.ExistsAsync(postId, ct))
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
