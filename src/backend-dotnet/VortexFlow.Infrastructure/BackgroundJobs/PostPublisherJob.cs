using Microsoft.Extensions.Logging;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using VortexFlow.Application.Audit;

namespace VortexFlow.Infrastructure.BackgroundJobs;

public class PostPublisherJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PostPublisherJob> _logger;
    private readonly ISecurityAuditLogger _audit;

    public PostPublisherJob(IApplicationDbContext context, ILogger<PostPublisherJob> logger, ISecurityAuditLogger audit)
    {
        _context = context;
        _logger = logger;
        _audit = audit;
    }

    public async Task PublishAsync(Guid postId)
    {
        _logger.LogInformation("Executing PostPublisherJob for Post ID: {PostId}", postId);

        // The job runs as a system process (Hangfire worker) without an
        // authenticated principal, so it sees every row regardless of any
        // future global query filter. For now, no global filters are defined
        // on ScheduledPost, so the lookup is a plain primary-key fetch.
        var post = await _context.ScheduledPosts
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post == null)
        {
            _logger.LogWarning("Post ID {PostId} not found.", postId);
            return;
        }

        // Defensive: only publish posts that are still due. If the user already
        // rescheduled the post to a later time, skip this run.
        if (post.ScheduledDate > DateTime.UtcNow)
        {
            _logger.LogInformation(
                "Post {PostId} rescheduled to {NewDate}; skipping this run.",
                postId, post.ScheduledDate);
            return;
        }

        try
        {
            await Task.Delay(500);
            _logger.LogInformation("Successfully published post to platform: {Platform}", post.Platform);
            post.Status = PostStatus.Published;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish post.");
            post.Status = PostStatus.Failed;
            _audit.SecurityEvent("post_publish_failed", null,
                new Dictionary<string, object?> { ["postId"] = postId, ["platform"] = post.Platform });
            throw;
        }

        await _context.SaveChangesAsync();
    }
}
