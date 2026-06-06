using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VortexFlow.Api.Metrics;
using VortexFlow.Application.Events;
using VortexFlow.Domain.Exceptions;
using VortexFlow.Infrastructure.Data;
using MassTransit;

namespace VortexFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrendsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IBus _bus;
    private readonly VortexFlowDbContext _context;
    private readonly AppMetrics _metrics;

    public TrendsController(
        IConfiguration configuration,
        IBus bus,
        VortexFlowDbContext context,
        AppMetrics metrics)
    {
        _configuration = configuration;
        _bus = bus;
        _context = context;
        _metrics = metrics;
    }

    /// <summary>
    /// Internal endpoint used by the Python worker to publish trend batches when
    /// the message broker is unavailable. Authenticated by a static API key in the
    /// <c>X-Api-Key</c> header; the key is validated against the
    /// <c>ApiKeys:Internal</c> configuration value which must be injected from
    /// the secret store in production.
    /// </summary>
    [HttpPost("ingest")]
    [Authorize(Policy = "InternalApiKey")]
    public async Task<IActionResult> Ingest([FromBody] List<TrendProcessedEvent> trends, CancellationToken ct)
    {
        if (trends is null || trends.Count == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["trends"] = new[] { "trends array must contain at least one event" } });
        }
        if (trends.Count > 1000)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["trends"] = new[] { "trends array must contain at most 1000 events per request" } });
        }

        foreach (var trend in trends)
        {
            await _bus.Publish(trend, ct);
            _metrics.RecordTrendIngested(trend.Platform);
        }
        return Accepted();
    }

    [HttpGet("current")]
    [Authorize]
    public async Task<IActionResult> GetCurrentTrends([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (limit is < 1 or > 200) limit = 50;

        var snapshots = await _context.TrendSnapshots
            .OrderByDescending(t => t.CapturedAt)
            .Take(limit)
            .ToListAsync(ct);

        var trends = snapshots.Select(t => new
        {
            EventId = t.Id,
            Platform = t.Platform,
            Hashtags = t.Hashtags,
            Metrics = t.Metrics is not null
                ? JsonSerializer.Deserialize<Dictionary<string, double>>(t.Metrics.RootElement.GetRawText(), (JsonSerializerOptions?)null)
                : new Dictionary<string, double>(),
            Timestamp = t.CapturedAt,
        });
        return Ok(new { trends });
    }
}
