using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using VortexFlow.Application.Auth;

namespace VortexFlow.Api.Authorization;

public static class JwtAuthorizationExtensions
{
    /// <summary>
    /// Adds a requirement to the JWT bearer pipeline that checks the token's
    /// <c>jti</c> against the revocation store. The token is considered invalid
    /// when the jti has been revoked (e.g. after logout, or after a forced
    /// revocation by an admin).
    /// </summary>
    public static AuthorizationPolicyBuilder RevokedTokenAware(this AuthorizationPolicyBuilder builder)
    {
        builder.AddRequirements(new RevokedTokenRequirement());
        return builder;
    }
}

public sealed class RevokedTokenRequirement : IAuthorizationRequirement { }

public sealed class RevokedTokenHandler : AuthorizationHandler<RevokedTokenRequirement>
{
    private readonly ITokenRevocationStore _store;
    private readonly IHttpContextAccessor _http;

    public RevokedTokenHandler(ITokenRevocationStore store, IHttpContextAccessor http)
    {
        _store = store;
        _http = http;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RevokedTokenRequirement requirement)
    {
        var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrEmpty(jti))
        {
            return;
        }
        if (!await _store.IsRevokedAsync(jti, _http.HttpContext?.RequestAborted ?? CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}
