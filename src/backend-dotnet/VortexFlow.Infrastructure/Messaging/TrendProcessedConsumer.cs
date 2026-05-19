using MassTransit;
using Microsoft.Extensions.Logging;
using VortexFlow.Application.Events;
using VortexFlow.Application.Interfaces;
using VortexFlow.Domain.Entities;
using System.Text.Json;
using VortexFlow.Application.Cache;
using Microsoft.AspNetCore.SignalR;
using VortexFlow.Infrastructure.Hubs;

namespace VortexFlow.Infrastructure.Messaging;

public class TrendProcessedConsumer : IConsumer<TrendProcessedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly ITrendCache _cache;
    private readonly IHubContext<TrendsHub> _hubContext;
    private readonly ILogger<TrendProcessedConsumer> _logger;

    public TrendProcessedConsumer(
        IApplicationDbContext context, 
        ITrendCache cache, 
        IHubContext<TrendsHub> hubContext,
        ILogger<TrendProcessedConsumer> logger)
    {
        _context = context;
        _cache = cache;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TrendProcessedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Received TrendProcessedEvent for platform {Platform}", message.Platform);

        // 1. Save to DB
        var snapshot = new TrendSnapshot
        {
            Id = message.EventId,
            Platform = message.Platform,
            Hashtags = message.Hashtags,
            Source = message.Source,
            CapturedAt = message.Timestamp,
            Metrics = JsonSerializer.SerializeToDocument(message.Metrics)
        };

        _context.TrendSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        // 2. Save to Cache
        string json = JsonSerializer.Serialize(message);
        foreach (var tag in message.Hashtags)
        {
            await _cache.SetTrendAsync(message.Platform, tag, json);
        }

        // 3. Notify via SignalR
        await _hubContext.Clients.All.SendAsync("TrendsUpdated", message);
    }
}
