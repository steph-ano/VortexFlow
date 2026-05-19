namespace VortexFlow.Application.Interfaces;

public interface IJobScheduler
{
    string SchedulePublishPostJob(Guid postId, DateTime scheduledDate);
}
