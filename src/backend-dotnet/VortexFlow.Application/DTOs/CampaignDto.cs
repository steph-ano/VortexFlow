namespace VortexFlow.Application.DTOs;

public class CampaignDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ScheduledPostDto
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
