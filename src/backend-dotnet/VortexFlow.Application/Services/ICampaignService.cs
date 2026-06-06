using VortexFlow.Application.DTOs;

namespace VortexFlow.Application.Services;

/// <summary>
/// Application service contract for campaigns and their scheduled posts.
///
/// <para><b>Security contract — Broken Object Level Authorization (IDOR).</b></para>
///
/// Every method that acts on a single, addressable resource takes the
/// authenticated <c>userId</c> of the caller as a <b>required</b> parameter.
/// The implementation is REQUIRED to:
///
/// <list type="number">
///   <item>Enforce ownership at the data layer by composing the resource id
///         with the caller's id in the WHERE clause (e.g.
///         <c>Where(p =&gt; p.Id == postId &amp;&amp; p.Campaign.OwnerId == userId)</c>).
///   </item>
///   <item>Distinguish "resource does not exist" (HTTP 404) from
///         "resource exists but the caller is not the owner" (HTTP 403)
///         by throwing <c>NotFoundException</c> and <c>ForbiddenException</c>
///         respectively.
///   </item>
///   <item>Emit an audit record via <c>ISecurityAuditLogger.AuthorizationDenied</c>
///         whenever ownership is denied, so SOC can detect probing attempts.
///   </item>
/// </list>
///
/// Callers MUST obtain <c>userId</c> exclusively from the authenticated
/// principal (JWT claims) and MUST NOT accept it from the request body or
/// query string. See <c>CampaignsController.RequireUserId</c>.
/// </summary>
public interface ICampaignService
{
    /// <summary>
    /// Creates a new campaign owned by <paramref name="ownerId"/>.
    /// </summary>
    Task<CampaignDto> CreateCampaignAsync(
        string name, string description, string ownerId, string? tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the campaigns owned by <paramref name="ownerId"/>. Never returns
    /// campaigns belonging to other users.
    /// </summary>
    Task<IEnumerable<CampaignDto>> GetCampaignsAsync(
        string ownerId, CancellationToken ct = default);

    /// <summary>
    /// Schedules a post for the campaign identified by <paramref name="campaignId"/>.
    /// </summary>
    /// <param name="campaignId">Id of the campaign the post will belong to.</param>
    /// <param name="userId">
    /// Authenticated id of the caller. The post is created only if the campaign
    /// is owned by this user; otherwise a <c>ForbiddenException</c> is thrown
    /// and the attempt is audited.
    /// </param>
    /// <exception cref="Domain.Exceptions.NotFoundException">Campaign does not exist.</exception>
    /// <exception cref="Domain.Exceptions.ForbiddenException">Campaign exists but is not owned by <paramref name="userId"/>.</exception>
    Task<ScheduledPostDto> SchedulePostAsync(
        Guid campaignId, string userId, string content, string platform, DateTime scheduledDate,
        CancellationToken ct = default);

    /// <summary>
    /// Re-queues a previously failed post for publishing.
    /// </summary>
    /// <param name="postId">Id of the post to retry.</param>
    /// <param name="userId">
    /// Authenticated id of the caller. The retry is performed only if the post's
    /// campaign is owned by this user.
    /// </param>
    /// <exception cref="Domain.Exceptions.NotFoundException">Post does not exist.</exception>
    /// <exception cref="Domain.Exceptions.ForbiddenException">Post exists but is not owned by <paramref name="userId"/>.</exception>
    Task<ScheduledPostDto> RetryPostAsync(
        Guid postId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Returns all posts whose parent campaign is owned by <paramref name="userId"/>.
    /// </summary>
    Task<IEnumerable<ScheduledPostDto>> GetPostsAsync(
        string userId, CancellationToken ct = default);

    /// <summary>
    /// Re-schedules an existing post to a new UTC date.
    /// </summary>
    /// <param name="postId">Id of the post to reschedule.</param>
    /// <param name="userId">
    /// Authenticated id of the caller. The reschedule is applied only if the
    /// post's campaign is owned by this user.
    /// </param>
    /// <exception cref="Domain.Exceptions.NotFoundException">Post does not exist.</exception>
    /// <exception cref="Domain.Exceptions.ForbiddenException">Post exists but is not owned by <paramref name="userId"/>.</exception>
    Task<ScheduledPostDto> ReschedulePostAsync(
        Guid postId, string userId, DateTime newDate, CancellationToken ct = default);
}
