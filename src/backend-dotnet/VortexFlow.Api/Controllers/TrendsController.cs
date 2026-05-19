using Microsoft.AspNetCore.Mvc;
using VortexFlow.Application.Events;
using MassTransit;
using VortexFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using VortexFlow.Api.Metrics;

namespace VortexFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrendsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IBus _bus;
    private readonly VortexFlowDbContext _context;
    private readonly AppMetrics _metrics;

    public TrendsController(IConfiguration configuration, IBus bus, VortexFlowDbContext context, AppMetrics metrics)
    {
        _configuration = configuration;
        _bus = bus;
        _context = context;
        _metrics = metrics;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromHeader(Name = "X-Api-Key")] string apiKey, [FromBody] List<TrendProcessedEvent> trends)
    {
        var expectedKey = _configuration["ApiKeys:Internal"];
        if (string.IsNullOrEmpty(expectedKey) || apiKey != expectedKey)
        {
            return Unauthorized();
        }

        foreach (var trend in trends)
        {
            await _bus.Publish(trend);
            _metrics.RecordTrendIngested(trend.Platform);
        }

        return Accepted();
    }

    [HttpGet("current")]
    [Authorize]
    public async Task<IActionResult> GetCurrentTrends()
    {
        var snapshots = await _context.TrendSnapshots
            .OrderByDescending(t => t.CapturedAt)
            .Take(50)
            .ToListAsync();

        var trends = snapshots.Select(t => new
        {
            EventId = t.Id,
            Platform = t.Platform,
            Hashtags = t.Hashtags,
            Metrics = t.Metrics is not null
                ? JsonSerializer.Deserialize<Dictionary<string, double>>(t.Metrics.RootElement.GetRawText(), (JsonSerializerOptions?)null)
                : new Dictionary<string, double>(),
            Timestamp = t.CapturedAt
        });

        return Ok(new { trends });
    }
}
