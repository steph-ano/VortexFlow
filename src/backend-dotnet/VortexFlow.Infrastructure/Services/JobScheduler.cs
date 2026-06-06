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
        var offset = new DateTimeOffset(DateTime.SpecifyKind(scheduledDate, DateTimeKind.Utc));
        return _backgroundJobClient.Schedule<BackgroundJobs.PostPublisherJob>(job => job.PublishAsync(postId), offset);
    }

    public bool CancelJob(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId)) return false;
        return _backgroundJobClient.Delete(jobId);
    }
}
