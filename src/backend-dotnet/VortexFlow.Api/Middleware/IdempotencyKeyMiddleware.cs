using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using VortexFlow.Application.Common;

namespace VortexFlow.Api.Middleware;

/// <summary>
/// Implements idempotency for non-idempotent endpoints. Clients send an
/// <c>Idempotency-Key</c> header; the first response is stored and replayed for
/// subsequent requests with the same (user, key, path, body) tuple.
///
/// <para><b>Security notes.</b></para>
/// <list type="bullet">
///   <item>The cache key includes the authenticated user id, so a response
///         generated for user A can never be replayed to user B even if both
///         send the same Idempotency-Key against the same path with the same
///         body. Without this scope, the middleware would leak cross-tenant
///         data (OWASP API1 / IDOR-like).</item>
///   <item>The response body is buffered and only copied to the original stream
///         inside a <c>try/finally</c>, so downstream <c>DomainException</c>s
///         still propagate to <c>ProblemDetailsExceptionMiddleware</c> and the
///         client receives a proper RFC 7807 error response.</item>
///   <item>The store is in-memory; for multi-replica deployments a Redis-backed
///         implementation MUST replace this. A background <see cref="Timer"/>
///         prunes expired entries every 5 minutes regardless of request load,
///         so the dictionary cannot grow unbounded under low traffic.</item>
/// </list>
/// </summary>
public sealed class IdempotencyKeyMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, IdempotencyRecord> _store = new();

    // Hard cap on stored entries. The store is in-process memory; if the
    // operator forgot to deploy a Redis-backed implementation, this prevents
    // an OOM crash. The cap is generous (50k entries) and is the last line of
    // defense; the primary protection is the 24h expiration and the timer.
    private const int MaxEntries = 50_000;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly Timer _sweeper = new(_ => SweepExpired(), null, SweepInterval, SweepInterval);

    private static readonly HashSet<string> IdempotentVerbs = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public IdempotencyKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IdempotentVerbs.Contains(context.Request.Method) ||
            !context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues))
        {
            await _next(context);
            return;
        }

        var key = keyValues.ToString();
        var userId = ResolveUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            // No identity -> never replay anything, never store anything.
            // Authenticated endpoints will reject this downstream.
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        var body = await ReadBodyAsync(context);

        var cacheKey = $"{userId}|{context.Request.Path}|{key}|{body}";

        if (_store.TryGetValue(cacheKey, out var record) && record.ExpiresAt > DateTime.UtcNow)
        {
            context.Response.StatusCode = record.StatusCode;
            context.Response.ContentType = record.ContentType;
            context.Response.Headers["Idempotency-Replay"] = "true";
            await context.Response.WriteAsync(record.Body);
            return;
        }

        // Buffer the response so we can both store it AND copy it to the
        // original stream. The finally block guarantees the original stream is
        // restored even when _next throws, so the OUTER
        // ProblemDetailsExceptionMiddleware can write the RFC 7807 body.
        var originalBodyStream = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        finally
        {
            // Copy the buffered response (success or partial) to the original
            // stream and restore the body so the outer middleware can still
            // write a ProblemDetails response if the controller threw after
            // _next returned control.
            if (buffer.Length > 0)
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBodyStream);
            }
            context.Response.Body = originalBodyStream;
        }

        // Only cache successful or deterministic-failure responses.
        // 5xx and exceptions should be retried by the client, not replayed.
        var status = context.Response.StatusCode;
        if (status >= 200 && status < 500)
        {
            buffer.Position = 0;
            var responseBody = await new StreamReader(buffer).ReadToEndAsync();

            // If the dictionary is at the cap, evict the oldest entry
            // opportunistically. Not strictly required (the timer also prunes)
            // but keeps the cap tight under burst load.
            if (_store.Count >= MaxEntries)
            {
                PruneOne();
            }

            _store[cacheKey] = new IdempotencyRecord(
                StatusCode: status,
                ContentType: context.Response.ContentType ?? "application/json",
                Body: responseBody,
                ExpiresAt: DateTime.UtcNow.Add(Retention));
        }
    }

    private static string? ResolveUserId(HttpContext ctx)
    {
        // Prefer the standard NameIdentifier claim. The JWT pipeline in this
        // app sets MapInboundClaims = false AND TokenService also emits both
        // JwtRegisteredClaimNames.Sub and ClaimTypes.NameIdentifier, so the
        // standard claim is always present for authenticated requests.
        return ctx.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? ctx.User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? ctx.User?.FindFirstValue("userId");
    }

    private static async Task<string> ReadBodyAsync(HttpContext ctx)
    {
        if (ctx.Request.ContentLength is null or 0) return string.Empty;
        using var sr = new StreamReader(ctx.Request.Body, leaveOpen: true);
        var body = await sr.ReadToEndAsync();
        ctx.Request.Body.Position = 0;
        return body;
    }

    private static void SweepExpired()
    {
        try
        {
            var now = DateTime.UtcNow;
            foreach (var kv in _store)
            {
                if (kv.Value.ExpiresAt < now)
                {
                    _store.TryRemove(kv.Key, out _);
                }
            }
        }
        catch
        {
            // The sweeper must never throw; the next tick will retry.
        }
    }

    private static void PruneOne()
    {
        // Evict the entry with the earliest ExpiresAt. _store is a
        // ConcurrentDictionary; we snapshot keys first to avoid mutating
        // during enumeration.
        string? victim = null;
        var earliest = DateTime.MaxValue;
        foreach (var kv in _store)
        {
            if (kv.Value.ExpiresAt < earliest)
            {
                earliest = kv.Value.ExpiresAt;
                victim = kv.Key;
            }
        }
        if (victim is not null)
        {
            _store.TryRemove(victim, out _);
        }
    }

    private readonly record struct IdempotencyRecord(int StatusCode, string ContentType, string Body, DateTime ExpiresAt);
}
