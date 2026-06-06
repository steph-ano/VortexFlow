namespace VortexFlow.Application.Interfaces;

public interface IJobScheduler
{
    /// <summary>
    /// Enqueues a Hangfire job that will publish a single ScheduledPost at the given time.
    /// </summary>
    string SchedulePublishPostJob(Guid postId, DateTime scheduledDate);

    /// <summary>
    /// Cancels a previously enqueued Hangfire job. Returns true if a job was found and removed.
    /// </summary>
    bool CancelJob(string jobId);
}
