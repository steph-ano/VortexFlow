using Hangfire.Dashboard;

namespace VortexFlow.Api.Authorization;

/// <summary>
/// Allows access to the Hangfire dashboard only for authenticated users in the
/// Admin role. Combine with an allowlist of CIDR ranges at the ingress for
/// defense in depth.
/// </summary>
public sealed class AdminDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var user = http.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }
        if (!user.IsInRole("Admin"))
        {
            return false;
        }
        return true;
    }
}
