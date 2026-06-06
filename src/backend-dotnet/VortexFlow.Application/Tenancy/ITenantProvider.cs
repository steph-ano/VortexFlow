using VortexFlow.Domain.Entities;

namespace VortexFlow.Application.Tenancy;

/// <summary>
/// Resolves the current tenant identifier for the executing request.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Returns the tenant id of the authenticated user, or null when the request is anonymous
    /// or the user has no tenant claim.
    /// </summary>
    string? GetCurrentTenantId();

    /// <summary>
    /// Returns the user id of the authenticated principal, or null when anonymous.
    /// </summary>
    string? GetCurrentUserId();
}
