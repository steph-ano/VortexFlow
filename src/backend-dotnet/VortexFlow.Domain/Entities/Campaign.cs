namespace VortexFlow.Domain.Entities;

public class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public User? Owner { get; set; }

    public ICollection<ScheduledPost> ScheduledPosts { get; set; } = new List<ScheduledPost>();
}
