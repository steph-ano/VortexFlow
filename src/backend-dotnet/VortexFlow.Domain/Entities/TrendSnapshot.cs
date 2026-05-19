using System.Text.Json;

namespace VortexFlow.Domain.Entities;

public class TrendSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Platform { get; set; } = string.Empty;
    public string[] Hashtags { get; set; } = Array.Empty<string>();
    
    // Representa la columna JSONB en base de datos
    public JsonDocument? Metrics { get; set; }
    
    public string Source { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
