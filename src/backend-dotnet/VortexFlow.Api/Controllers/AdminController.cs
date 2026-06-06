using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hangfire;

namespace VortexFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    [HttpGet("dead-letters")]
    public IActionResult GetDeadLetters()
    {
        var monitoringApi = JobStorage.Current.GetMonitoringApi();
        var failedJobs = monitoringApi.FailedJobs(0, 100);
        var result = failedJobs.Select(j => new
        {
            JobId = j.Key,
            JobType = j.Value.Job.Type.Name,
            Method = j.Value.Job.Method.Name,
            FailedAt = j.Value.FailedAt,
            ExceptionType = j.Value.ExceptionType,
            ExceptionMessage = j.Value.ExceptionMessage,
        });
        return Ok(result);
    }

    [HttpPost("jobs/{jobId}/retry")]
    public IActionResult RetryJob(string jobId)
    {
        var requeued = JobStorage.Current.GetConnection().CreateWriteTransaction();
        // Hangfire's transactional retry is not universally available; safer is to
        // re-enqueue. We keep the call idempotent at the dashboard level.
        BackgroundJob.Requeue(jobId);
        return Accepted(new { requeued = true, jobId });
    }
}
