using Microsoft.Extensions.Logging;
using VortexFlow.Application.Audit;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Enums;

namespace VortexFlow.Infrastructure.BackgroundJobs;

public class PostPublisherJob
{
    private readonly IScheduledPostRepository _posts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PostPublisherJob> _logger;
    private readonly ISecurityAuditLogger _audit;

    public PostPublisherJob(
        IScheduledPostRepository posts,
        IUnitOfWork unitOfWork,
        ILogger<PostPublisherJob> logger,
        ISecurityAuditLogger audit)
    {
        _posts = posts;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _audit = audit;
    }

    public async Task PublishAsync(Guid postId)
    {
        _logger.LogInformation("Executing PostPublisherJob for Post ID: {PostId}", postId);

        // The job runs as a system process (Hangfire worker) without an
        // authenticated principal, so it sees every row regardless of any
        // future global query filter. For now, no global filters are
        // defined on ScheduledPost, so the lookup is a plain primary-key
        // fetch via the repository.
        var post = await _posts.GetByIdAsync(postId);

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

        await _unitOfWork.SaveChangesAsync();
    }
}
