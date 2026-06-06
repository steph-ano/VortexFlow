namespace VortexFlow.Application.Auth;

/// <summary>
/// Persists revoked JWT identifiers (jti) and refresh-token identifiers so they
/// can be rejected by the bearer middleware. Implementations should expire entries
/// automatically when the underlying token would have expired.
/// </summary>
public interface ITokenRevocationStore
{
    Task RevokeAsync(string jti, TimeSpan ttl, string? userId = null, string? reason = null, CancellationToken ct = default);
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);
}
