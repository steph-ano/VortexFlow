using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VortexFlow.Application.DTOs;
using VortexFlow.Application.Services;
using System.Security.Claims;
using VortexFlow.Api.Metrics;

namespace VortexFlow.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly AppMetrics _metrics;

    public CampaignsController(ICampaignService campaignService, AppMetrics metrics)
    {
        _campaignService = campaignService;
        _metrics = metrics;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var campaign = await _campaignService.CreateCampaignAsync(request.Name, request.Description, userId);
        return Ok(campaign);
    }

    [HttpGet]
    public async Task<IActionResult> GetCampaigns()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var campaigns = await _campaignService.GetCampaignsAsync(userId);
        return Ok(campaigns);
    }

    [HttpPost("{id}/posts")]
    public async Task<IActionResult> SchedulePost(Guid id, [FromBody] SchedulePostRequest request)
    {
        var post = await _campaignService.SchedulePostAsync(id, request.Content, request.Platform, request.ScheduledDate);
        return Ok(post);
    }

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var posts = await _campaignService.GetPostsAsync(userId);
        return Ok(posts);
    }

    [HttpPut("posts/{postId}/reschedule")]
    public async Task<IActionResult> ReschedulePost(Guid postId, [FromBody] ReschedulePostRequest request)
    {
        var post = await _campaignService.ReschedulePostAsync(postId, request.Date);
        return Ok(post);
    }

    [HttpPost("posts/{postId}/retry")]
    public async Task<IActionResult> RetryPost(Guid postId)
    {
        try
        {
            var post = await _campaignService.RetryPostAsync(postId);
            _metrics.RecordPostPublished(post.Platform, true);
            return Ok(post);
        }
        catch
        {
            _metrics.RecordPostPublished("unknown", false);
            throw;
        }
    }
}

public class ReschedulePostRequest
{
    public DateTime Date { get; set; }
}

public class CreateCampaignRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SchedulePostRequest
{
    public string Content { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
}
