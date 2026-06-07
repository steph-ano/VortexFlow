using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VortexFlow.Api.Metrics;
using VortexFlow.Api.Validation;
using VortexFlow.Application.Services;
using VortexFlow.Application.Tenancy;
using VortexFlow.Domain.Exceptions;
// Disambiguate: System.ComponentModel.DataAnnotations also defines ValidationException.
// The application-wide domain exception must take precedence so ProblemDetailsExceptionMiddleware
// can map it to RFC 7807.
using ValidationException = VortexFlow.Domain.Exceptions.ValidationException;

namespace VortexFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly AppMetrics _metrics;
    private readonly ITenantProvider _tenantProvider;

    public CampaignsController(
        ICampaignService campaignService,
        AppMetrics metrics,
        ITenantProvider tenantProvider)
    {
        _campaignService = campaignService;
        _metrics = metrics;
        _tenantProvider = tenantProvider;
    }

    [HttpPost]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var errors = ValidateRequest(request);
        if (errors.Count > 0) throw new ValidationException(errors);

        var userId = RequireUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var campaign = await _campaignService.CreateCampaignAsync(request.Name, request.Description, userId, tenantId, ct);
        return CreatedAtAction(nameof(GetCampaigns), new { }, campaign);
    }

    [HttpGet]
    public async Task<IActionResult> GetCampaigns(CancellationToken ct)
    {
        var userId = RequireUserId();
        var campaigns = await _campaignService.GetCampaignsAsync(userId, ct);
        return Ok(campaigns);
    }

    [HttpPost("{id:guid}/posts")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> SchedulePost(Guid id, [FromBody] SchedulePostRequest request, CancellationToken ct)
    {
        var errors = ValidateRequest(request);
        if (errors.Count > 0) throw new ValidationException(errors);
        var userId = RequireUserId();
        var post = await _campaignService.SchedulePostAsync(id, userId, request.Content, request.Platform, request.ScheduledDate, ct);
        return Created($"/api/campaigns/posts/{post.Id}", post);
    }

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts(CancellationToken ct)
    {
        var userId = RequireUserId();
        var posts = await _campaignService.GetPostsAsync(userId, ct);
        return Ok(posts);
    }

    [HttpPut("posts/{postId:guid}/reschedule")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> ReschedulePost(Guid postId, [FromBody] ReschedulePostRequest request, CancellationToken ct)
    {
        if (request is null) throw new ValidationException(new Dictionary<string, string[]> { ["body"] = new[] { "Request body required." } });
        if (request.Date == default) throw new ValidationException(new Dictionary<string, string[]> { ["date"] = new[] { "date is required" } });
        var userId = RequireUserId();
        var post = await _campaignService.ReschedulePostAsync(postId, userId, request.Date, ct);
        return Ok(post);
    }

    [HttpPost("posts/{postId:guid}/retry")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> RetryPost(Guid postId, CancellationToken ct)
    {
        var userId = RequireUserId();
        try
        {
            var post = await _campaignService.RetryPostAsync(postId, userId, ct);
            _metrics.RecordPostPublished(post.Platform, true);
            return Ok(post);
        }
        catch
        {
            _metrics.RecordPostPublished("unknown", false);
            throw;
        }
    }

    /// <summary>
    /// Extracts the authenticated user's id from the JWT claims.
    ///
    /// The JWT bearer pipeline is configured with <c>MapInboundClaims = false</c>
    /// (see ServiceCollectionExtensions.AddVortexFlowAuth), which means the raw
    /// <c>sub</c> claim is NOT rewritten to <see cref="ClaimTypes.NameIdentifier"/>.
    /// Therefore we MUST probe both forms, in order:
    ///   1. <c>ClaimTypes.NameIdentifier</c>  – when the issuer maps <c>sub</c> on its side.
    ///   2. <c>JwtRegisteredClaimNames.Sub</c> – raw <c>sub</c> claim, default for our tokens.
    ///   3. <c>"userId"</c>                   – custom claim for non-standard issuers.
    ///
    /// This method is the single source of truth for "who is calling" inside the
    /// Campaigns API. All IDOR-sensitive endpoints call it before any service
    /// invocation; the value is forwarded to the service which then validates
    /// ownership of every resource it touches.
    /// </summary>
    /// <exception cref="ForbiddenException">
    /// Thrown when no recognizable user-id claim is present. This is treated as
    /// 403 (not 401) because the request did reach an <c>[Authorize]</c> endpoint,
    /// meaning the JWT was validated but lacks the required subject.
    /// </exception>
    private string RequireUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue("userId")
        ?? throw new ForbiddenException("Authenticated request is missing a user-id claim.");

    private static IDictionary<string, string[]> ValidateRequest(CreateCampaignRequest? r)
    {
        var errors = new Dictionary<string, string[]>();
        if (r is null) { errors["body"] = new[] { "body required" }; return errors; }
        if (string.IsNullOrWhiteSpace(r.Name)) errors["name"] = new[] { "name is required" };
        if (r.Name?.Length > 120) errors["name"] = new[] { "name must be <= 120 chars" };
        if (r.Description?.Length > 1024) errors["description"] = new[] { "description must be <= 1024 chars" };
        return errors;
    }

    private static IDictionary<string, string[]> ValidateRequest(SchedulePostRequest? r)
    {
        var errors = new Dictionary<string, string[]>();
        if (r is null) { errors["body"] = new[] { "body required" }; return errors; }
        if (string.IsNullOrWhiteSpace(r.Content)) errors["content"] = new[] { "content is required" };
        if (r.Content?.Length > 4096) errors["content"] = new[] { "content must be <= 4096 chars" };
        if (string.IsNullOrWhiteSpace(r.Platform)) errors["platform"] = new[] { "platform is required" };
        if (r.ScheduledDate == default) errors["scheduledDate"] = new[] { "scheduledDate is required" };
        return errors;
    }
}

public class ReschedulePostRequest
{
    [Required, FutureUtcDate] public DateTime Date { get; set; }
}

public class CreateCampaignRequest
{
    [Required, StringLength(120, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [StringLength(1024)] public string Description { get; set; } = string.Empty;
}

public class SchedulePostRequest
{
    [Required, StringLength(4096, MinimumLength = 1)] public string Content { get; set; } = string.Empty;
    [Required, StringLength(64, MinimumLength = 1)] public string Platform { get; set; } = string.Empty;
    [Required, FutureUtcDate] public DateTime ScheduledDate { get; set; }
}
