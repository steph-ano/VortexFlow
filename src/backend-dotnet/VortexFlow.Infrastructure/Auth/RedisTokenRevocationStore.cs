using StackExchange.Redis;
using VortexFlow.Application.Auth;

namespace VortexFlow.Infrastructure.Auth;

/// <summary>
/// Stores revoked JWT identifiers in Redis. The key carries the same TTL as the
/// token so the denylist self-cleans and never grows unbounded.
/// </summary>
public sealed class RedisTokenRevocationStore : ITokenRevocationStore
{
    private readonly IConnectionMultiplexer _redis;
    private const string Prefix = "jwt:revoked:";

    public RedisTokenRevocationStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task RevokeAsync(string jti, TimeSpan ttl, string? userId = null, string? reason = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(jti))
        {
            throw new ArgumentException("jti is required", nameof(jti));
        }
        var db = _redis.GetDatabase();
        await db.StringSetAsync(Prefix + jti, reason ?? "revoked", ttl);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(jti))
        {
            return false;
        }
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync(Prefix + jti);
    }
}
