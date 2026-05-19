using VortexFlow.Domain.Enums;

namespace VortexFlow.Domain.Entities;

public class ScheduledPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public PostStatus Status { get; set; } = PostStatus.Pending;
    public string? HangfireJobId { get; set; }
    public string? TenantId { get; set; }
}
