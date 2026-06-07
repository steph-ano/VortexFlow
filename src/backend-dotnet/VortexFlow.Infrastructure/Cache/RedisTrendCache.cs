using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;
using VortexFlow.Application.Cache;
using VortexFlow.Application.Interfaces;

namespace VortexFlow.Infrastructure.Cache;

public class RedisTrendCache : ITrendCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTrendCache> _logger;
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceScopeFactory _scopeFactory;

    public RedisTrendCache(
        IConnectionMultiplexer redis,
        ILogger<RedisTrendCache> logger,
        IMemoryCache memoryCache,
        IServiceScopeFactory scopeFactory)
    {
        _redis = redis;
        _logger = logger;
        _memoryCache = memoryCache;
        _scopeFactory = scopeFactory;

        _circuitBreaker = Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30),
                onBreak: (ex, breakDelay) => _logger.LogWarning("Redis circuit broken for {Delay}s due to {Ex}", breakDelay.TotalSeconds, ex.Message),
                onReset: () => _logger.LogInformation("Redis circuit reset"),
                onHalfOpen: () => _logger.LogInformation("Redis circuit half-open"));
    }

    public async Task SetTrendAsync(string platform, string hashtag, string data, TimeSpan? expiry = null)
    {
        try
        {
            await _circuitBreaker.ExecuteAsync(async () =>
            {
                var db = _redis.GetDatabase();
                await db.StringSetAsync($"trend:current:{platform}:{hashtag}", data, expiry ?? TimeSpan.FromMinutes(5));
            });
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Cannot write to Redis, circuit is open. Saving to MemoryCache as fallback.");
            _memoryCache.Set($"trend:current:{platform}:{hashtag}", data, TimeSpan.FromMinutes(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write to Redis (circuit may not be fully open yet).");
            _memoryCache.Set($"trend:current:{platform}:{hashtag}", data, TimeSpan.FromMinutes(1));
        }
    }

    public async Task<string?> GetTrendAsync(string platform, string hashtag)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                var db = _redis.GetDatabase();
                return await db.StringGetAsync($"trend:current:{platform}:{hashtag}");
            });
        }
        catch (Exception ex) when (ex is BrokenCircuitException || ex is RedisConnectionException || ex is RedisTimeoutException)
        {
            _logger.LogWarning("Redis is unavailable. Falling back to MemoryCache & PostgreSQL for {Platform}:{Hashtag}.", platform, hashtag);
            return await GetFallbackTrendAsync(platform, hashtag);
        }
    }

    private async Task<string?> GetFallbackTrendAsync(string platform, string hashtag)
    {
        var cacheKey = $"trend:current:{platform}:{hashtag}";
        if (_memoryCache.TryGetValue(cacheKey, out string? cachedData))
        {
            return cachedData;
        }

        // The cache is a singleton, so the repository (scoped) must be resolved
        // per-call through a fresh DI scope. This keeps the cache isolated from
        // the unit-of-work lifecycle of any one request.
        using var scope = _scopeFactory.CreateScope();
        var snapshots = scope.ServiceProvider.GetRequiredService<ITrendSnapshotRepository>();

        var latestTrend = await snapshots.FindLatestByPlatformAndHashtagAsync(platform, hashtag);

        if (latestTrend != null)
        {
            var data = JsonSerializer.Serialize(new
            {
                EventId = latestTrend.Id,
                Platform = latestTrend.Platform,
                Hashtags = latestTrend.Hashtags,
                Metrics = latestTrend.Metrics != null ? JsonSerializer.Deserialize<Dictionary<string, double>>(latestTrend.Metrics.RootElement.GetRawText(), (JsonSerializerOptions?)null) : new Dictionary<string, double>(),
                Timestamp = latestTrend.CapturedAt
            });

            _memoryCache.Set(cacheKey, data, TimeSpan.FromMinutes(1));
            return data;
        }

        return null;
    }
}
