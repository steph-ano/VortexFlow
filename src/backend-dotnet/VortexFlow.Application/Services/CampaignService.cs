using VortexFlow.Application.DTOs;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Entities;
using VortexFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace VortexFlow.Application.Services;

public class CampaignService : ICampaignService
{
    private readonly IApplicationDbContext _context;
    private readonly IJobScheduler _jobScheduler;

    public CampaignService(IApplicationDbContext context, IJobScheduler jobScheduler)
    {
        _context = context;
        _jobScheduler = jobScheduler;
    }

    public async Task<CampaignDto> CreateCampaignAsync(string name, string description, string ownerId)
    {
        var campaign = new Campaign
        {
            Name = name,
            Description = description,
            OwnerId = ownerId
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        return new CampaignDto
        {
            Id = campaign.Id,
            Name = campaign.Name,
            Description = campaign.Description
        };
    }

    public async Task<IEnumerable<CampaignDto>> GetCampaignsAsync(string ownerId)
    {
        return await _context.Campaigns
            .Where(c => c.OwnerId == ownerId)
            .Select(c => new CampaignDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description
            }).ToListAsync();
    }

    public async Task<ScheduledPostDto> SchedulePostAsync(Guid campaignId, string content, string platform, DateTime scheduledDate)
    {
        if (scheduledDate <= DateTime.UtcNow)
        {
            throw new ArgumentException("Scheduled date must be in the future.");
        }

        var post = new ScheduledPost
        {
            CampaignId = campaignId,
            Content = content,
            Platform = platform,
            ScheduledDate = scheduledDate,
            Status = PostStatus.Pending
        };

        _context.ScheduledPosts.Add(post);
        await _context.SaveChangesAsync();
        
        post.HangfireJobId = _jobScheduler.SchedulePublishPostJob(post.Id, scheduledDate);
        await _context.SaveChangesAsync();

        return new ScheduledPostDto
        {
            Id = post.Id,
            CampaignId = post.CampaignId,
            Content = post.Content,
            Platform = post.Platform,
            ScheduledDate = post.ScheduledDate,
            Status = post.Status.ToString()
        };
    }

    public async Task<ScheduledPostDto> RetryPostAsync(Guid postId)
    {
        var post = await _context.ScheduledPosts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null) throw new Exception("Post not found");
        
        post.Status = PostStatus.Pending;
        
        var newDate = post.ScheduledDate > DateTime.UtcNow ? post.ScheduledDate : DateTime.UtcNow.AddMinutes(5);
        post.ScheduledDate = newDate;
        post.HangfireJobId = _jobScheduler.SchedulePublishPostJob(post.Id, newDate);
        
        await _context.SaveChangesAsync();
        
        return MapToDto(post);
    }

    public async Task<IEnumerable<ScheduledPostDto>> GetPostsAsync(string userId)
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
                Status = p.Status.ToString()
            })
            .ToListAsync();
    }

    public async Task<ScheduledPostDto> ReschedulePostAsync(Guid postId, DateTime newDate)
    {
        var post = await _context.ScheduledPosts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null) throw new Exception("Post not found");

        post.ScheduledDate = newDate;
        await _context.SaveChangesAsync();

        return MapToDto(post);
    }

    private static ScheduledPostDto MapToDto(ScheduledPost post)
    {
        return new ScheduledPostDto
        {
            Id = post.Id,
            CampaignId = post.CampaignId,
            Content = post.Content,
            Platform = post.Platform,
            ScheduledDate = post.ScheduledDate,
            Status = post.Status.ToString()
        };
    }
}
