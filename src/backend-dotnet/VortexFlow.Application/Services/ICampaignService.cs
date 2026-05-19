using VortexFlow.Application.DTOs;
using VortexFlow.Domain.Entities;

namespace VortexFlow.Application.Services;

public interface ICampaignService
{
    Task<CampaignDto> CreateCampaignAsync(string name, string description, string ownerId);
    Task<IEnumerable<CampaignDto>> GetCampaignsAsync(string ownerId);
    Task<ScheduledPostDto> SchedulePostAsync(Guid campaignId, string content, string platform, DateTime scheduledDate);
    Task<ScheduledPostDto> RetryPostAsync(Guid postId);
    Task<IEnumerable<ScheduledPostDto>> GetPostsAsync(string userId);
    Task<ScheduledPostDto> ReschedulePostAsync(Guid postId, DateTime newDate);
}
