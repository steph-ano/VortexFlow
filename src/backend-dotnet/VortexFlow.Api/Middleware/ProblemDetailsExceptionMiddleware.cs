using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VortexFlow.Domain.Exceptions;

namespace VortexFlow.Api.Middleware;

/// <summary>
/// Translates DomainException subclasses into RFC 7807 ProblemDetails responses and
/// logs every unhandled exception with the correlation id. Anything not mapped is
/// surfaced as a 500 with a generic detail.
/// </summary>
public sealed class ProblemDetailsExceptionMiddleware
{
    private const string CorrelationIdItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    public ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex) { await WriteAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message); }
        catch (ForbiddenException ex) { await WriteAsync(context, StatusCodes.Status403Forbidden, "Forbidden", ex.Message); }
        catch (ValidationException ex)
        {
            await WriteValidationAsync(context, ex);
        }
        catch (ConflictException ex) { await WriteAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message); }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var correlationId = context.Items[CorrelationIdItemKey] as string ?? "n/a";
            _logger.LogError(ex, "Unhandled exception on {Method} {Path} (correlationId={CorrelationId})",
                context.Request.Method, context.Request.Path, correlationId);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred. Reference the correlation id when reporting this issue.");
        }
    }

    private static Task WriteAsync(HttpContext ctx, int status, string title, string detail)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var problem = new
        {
            type = $"https://httpstatuses.io/{status}",
            title,
            status,
            detail,
            traceId = ctx.TraceIdentifier,
            correlationId = ctx.Items[CorrelationIdItemKey] as string,
        };
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private static Task WriteValidationAsync(HttpContext ctx, ValidationException ex)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        ctx.Response.ContentType = "application/problem+json";
        var problem = new
        {
            type = "https://httpstatuses.io/400",
            title = "Validation failed",
            status = 400,
            errors = ex.Errors,
            traceId = ctx.TraceIdentifier,
            correlationId = ctx.Items[CorrelationIdItemKey] as string,
        };
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
