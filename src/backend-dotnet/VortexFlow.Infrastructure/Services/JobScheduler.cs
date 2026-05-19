using Hangfire;
using VortexFlow.Application.Interfaces;

namespace VortexFlow.Infrastructure.Services;

public class JobScheduler : IJobScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public JobScheduler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public string SchedulePublishPostJob(Guid postId, DateTime scheduledDate)
    {
        // Encola el trabajo
        var offset = new DateTimeOffset(scheduledDate);
        // Note: we will need to reference PostPublisherJob, it's in Infrastructure, but we can call it 
        return _backgroundJobClient.Schedule<BackgroundJobs.PostPublisherJob>(job => job.PublishAsync(postId), offset);
    }
}
