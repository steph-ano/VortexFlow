using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VortexFlow.Application.Audit;
using VortexFlow.Application.Cache;
using VortexFlow.Application.Events;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Entities;
using VortexFlow.Infrastructure.Hubs;

namespace VortexFlow.Infrastructure.Messaging;

/// <summary>
/// Consumes TrendProcessedEvent messages. Idempotent: when the same EventId is
/// seen twice, the second delivery is a no-op so re-deliveries from the broker
/// or HTTP fallback do not create duplicate snapshots.
/// </summary>
public class TrendProcessedConsumer : IConsumer<TrendProcessedEvent>
{
    private readonly ITrendSnapshotRepository _snapshots;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITrendCache _cache;
    private readonly IHubContext<TrendsHub> _hubContext;
    private readonly ILogger<TrendProcessedConsumer> _logger;
    private readonly ISecurityAuditLogger _audit;

    public TrendProcessedConsumer(
        ITrendSnapshotRepository snapshots,
        IUnitOfWork unitOfWork,
        ITrendCache cache,
        IHubContext<TrendsHub> hubContext,
        ILogger<TrendProcessedConsumer> logger,
        ISecurityAuditLogger audit)
    {
        _snapshots = snapshots;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _hubContext = hubContext;
        _logger = logger;
        _audit = audit;
    }

    public async Task Consume(ConsumeContext<TrendProcessedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Received TrendProcessedEvent {EventId} for platform {Platform}",
            message.EventId, message.Platform);

        // Idempotency: skip if the snapshot already exists. When a global
        // tenant filter is later added to TrendSnapshot, the consumer
        // must still see cross-tenant rows because the broker event is
        // the source of truth; the repository's FindByEventIdAsync is
        // expected to be the seam for that future behavior.
        var existing = await _snapshots.FindByEventIdAsync(message.EventId, context.CancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Snapshot {EventId} already processed; skipping.", message.EventId);
            return;
        }

        var snapshot = new TrendSnapshot
        {
            Id = message.EventId,
            Platform = message.Platform,
            Hashtags = message.Hashtags,
            Source = message.Source,
            CapturedAt = message.Timestamp == default ? DateTime.UtcNow : message.Timestamp,
            Metrics = message.Metrics is null
                ? null
                : JsonSerializer.SerializeToDocument(new
                {
                    message.Metrics.Volume,
                    message.Metrics.Sentiment,
                }),
        };

        _snapshots.Add(snapshot);
        await _unitOfWork.SaveChangesAsync(context.CancellationToken);

        // Best-effort cache + signalr. Failures here must not poison the message.
        try
        {
            var json = JsonSerializer.Serialize(message);
            foreach (var tag in message.Hashtags)
            {
                await _cache.SetTrendAsync(message.Platform, tag, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for {EventId}; continuing.", message.EventId);
        }
        try
        {
            await _hubContext.Clients.All.SendAsync("TrendsUpdated", message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR broadcast failed for {EventId}; continuing.", message.EventId);
        }
    }
}
