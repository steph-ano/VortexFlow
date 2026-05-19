using System.Diagnostics.Metrics;

namespace VortexFlow.Api.Metrics;

public class AppMetrics
{
    public static readonly string MeterName = "VortexFlow.Api";

    private readonly Counter<long> _trendsIngested;
    private readonly Counter<long> _postsPublished;
    private readonly Counter<long> _postsFailed;
    private readonly UpDownCounter<long> _circuitBreakerState;

    public AppMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _trendsIngested = meter.CreateCounter<long>(
            "vortexflow_trends_ingested_total",
            description: "Total number of trends ingested");

        _postsPublished = meter.CreateCounter<long>(
            "vortexflow_posts_published_total",
            description: "Total number of posts published");

        _postsFailed = meter.CreateCounter<long>(
            "vortexflow_posts_failed_total",
            description: "Total number of posts that failed");

        _circuitBreakerState = meter.CreateUpDownCounter<long>(
            "vortexflow_circuit_breaker_state",
            description: "Circuit breaker state (0=Closed, 1=Open, 2=HalfOpen)");
    }

    public void RecordTrendIngested(string platform) =>
        _trendsIngested.Add(1, new KeyValuePair<string, object?>("platform", platform));

    public void RecordPostPublished(string platform, bool success)
    {
        if (success)
            _postsPublished.Add(1, new KeyValuePair<string, object?>("platform", platform));
        else
            _postsFailed.Add(1, new KeyValuePair<string, object?>("platform", platform));
    }

    public void SetCircuitBreakerState(string breaker, int state) =>
        _circuitBreakerState.Add(state, new KeyValuePair<string, object?>("breaker", breaker));
}
