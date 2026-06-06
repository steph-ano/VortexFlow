using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace VortexFlow.Application.Audit;

/// <summary>
/// Records security-relevant events to the structured log. Use this for logins
/// (success/failure), ownership denials, admin actions, and token revocations.
/// All events carry a structured <c>audit</c> state with the acting user id,
/// source IP, correlation id, and event name so they can be queried centrally.
/// </summary>
public interface ISecurityAuditLogger
{
    void LoginSuccess(string userId, string email, string? ip);
    void LoginFailure(string email, string reason, string? ip);
    void AuthorizationDenied(string userId, string resource, string resourceKey, string? ip);
    void AdminAction(string userId, string action, string? target, string? ip);
    void TokenRevoked(string userId, string jti, string? reason, string? ip);
    void SecurityEvent(string eventName, string? userId, IReadOnlyDictionary<string, object?>? properties = null);
}

public sealed class SecurityAuditLogger : ISecurityAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;
    public SecurityAuditLogger(ILogger<SecurityAuditLogger> logger) => _logger = logger;

    public void LoginSuccess(string userId, string email, string? ip) =>
        Write("login_success", userId, new Dictionary<string, object?> { ["email"] = email, ["ip"] = ip });

    public void LoginFailure(string email, string reason, string? ip) =>
        Write("login_failure", null, new Dictionary<string, object?> { ["email"] = email, ["reason"] = reason, ["ip"] = ip });

    public void AuthorizationDenied(string userId, string resource, string resourceKey, string? ip) =>
        Write("authorization_denied", userId, new Dictionary<string, object?> { ["resource"] = resource, ["key"] = resourceKey, ["ip"] = ip });

    public void AdminAction(string userId, string action, string? target, string? ip) =>
        Write("admin_action", userId, new Dictionary<string, object?> { ["action"] = action, ["target"] = target, ["ip"] = ip });

    public void TokenRevoked(string userId, string jti, string? reason, string? ip) =>
        Write("token_revoked", userId, new Dictionary<string, object?> { ["jti"] = jti, ["reason"] = reason, ["ip"] = ip });

    public void SecurityEvent(string eventName, string? userId, IReadOnlyDictionary<string, object?>? properties = null) =>
        Write(eventName, userId, properties);

    private void Write(string eventName, string? userId, IReadOnlyDictionary<string, object?>? properties)
    {
        // Structured log scope consumed by Loki/Serilog OTLP. Using BeginScope so the
        // properties become searchable labels.
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["audit"] = true,
            ["event"] = eventName,
            ["userId"] = userId ?? "anonymous",
            ["properties"] = properties ?? new Dictionary<string, object?>(),
        });
        _logger.LogInformation("security event {Event} for {UserId}", eventName, userId ?? "anonymous");
    }
}

public static class SecurityAuditHttpContextExtensions
{
    public static string? ClientIp(this HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString()
        ?? ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
}
