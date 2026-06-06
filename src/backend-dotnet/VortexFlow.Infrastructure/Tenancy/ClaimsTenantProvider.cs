using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VortexFlow.Application.Tenancy;

namespace VortexFlow.Infrastructure.Tenancy;

/// <summary>
/// Extracts the current tenant id and user id from the request's ClaimsPrincipal.
/// The tenant id is read from a "tenant_id" claim. Single-tenant installs may not issue
/// the claim, in which case GetCurrentTenantId returns null and global query filters
/// fall back to the legacy single-tenant model.
/// </summary>
public sealed class ClaimsTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentTenantId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }
        return user.FindFirstValue("tenant_id");
    }

    public string? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
