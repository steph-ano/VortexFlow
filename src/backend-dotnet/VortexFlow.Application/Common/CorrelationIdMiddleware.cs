using Microsoft.AspNetCore.Http;

namespace VortexFlow.Application.Common;

public static class CorrelationIdMiddleware
{
    public const string HttpContextKey = "CorrelationId";
    public const string HeaderName = "X-Correlation-Id";

    public static string Ensure(HttpContext context)
    {
        if (context.Items.TryGetValue(HttpContextKey, out var existing) && existing is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }
        var fromHeader = context.Request.Headers[HeaderName].FirstOrDefault();
        var id = string.IsNullOrWhiteSpace(fromHeader) ? Guid.NewGuid().ToString("N") : fromHeader;
        context.Items[HttpContextKey] = id;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers[HeaderName] = id;
            }
            return Task.CompletedTask;
        });
        return id;
    }
}
