using Microsoft.AspNetCore.Http;

namespace VortexFlow.Api.Middleware;

/// <summary>
/// Ensures every request has a correlation id (echoed in the X-Correlation-Id
/// response header and exposed to downstream code via HttpContext.Items).
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        VortexFlow.Application.Common.CorrelationIdMiddleware.Ensure(context);
        return _next(context);
    }
}
