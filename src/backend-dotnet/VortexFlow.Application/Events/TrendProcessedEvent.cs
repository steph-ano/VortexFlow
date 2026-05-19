using System.Text.Json;

namespace VortexFlow.Application.Events;

public class TrendProcessedEvent
{
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public string[] Hashtags { get; set; } = Array.Empty<string>();
    public TrendMetrics Metrics { get; set; } = new();
    public string Platform { get; set; } = string.Empty;
    public JsonElement? RawData { get; set; }
}

public class TrendMetrics
{
    public int Volume { get; set; }
    public double Sentiment { get; set; }
}
