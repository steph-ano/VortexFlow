using Microsoft.Extensions.Logging;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace VortexFlow.Infrastructure.BackgroundJobs;

public class PostPublisherJob
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PostPublisherJob> _logger;

    public PostPublisherJob(IApplicationDbContext context, ILogger<PostPublisherJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task PublishAsync(Guid postId)
    {
        _logger.LogInformation("Executing PostPublisherJob for Post ID: {PostId}", postId);

        var post = await _context.ScheduledPosts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null)
        {
            _logger.LogWarning("Post ID {PostId} not found.", postId);
            return;
        }

        try
        {
            // Simulate publishing
            await Task.Delay(500); 
            _logger.LogInformation("Successfully published post to platform: {Platform}", post.Platform);
            
            post.Status = PostStatus.Published;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish post.");
            post.Status = PostStatus.Failed;
            throw; // This will tell Hangfire to retry
        }
        
        await _context.SaveChangesAsync();
    }
}
