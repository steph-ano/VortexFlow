using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using VortexFlow.Api.Authorization;
using VortexFlow.Application.Audit;
using VortexFlow.Application.Auth;
using VortexFlow.Application.Tenancy;
using VortexFlow.Domain.Entities;
using VortexFlow.Infrastructure.Auth;
using VortexFlow.Infrastructure.Data;
using VortexFlow.Infrastructure.Tenancy;

namespace VortexFlow.Api.Extensions;

/// <summary>
/// Authentication and authorization bootstrap.
///
/// Wires four concerns that must stay together for security correctness:
///   1. ASP.NET Core Identity (password policy, lockout, EF stores).
///   2. JWT bearer scheme with revocation-store integration in the
///      <c>OnTokenValidated</c> event.
///   3. The internal <c>X-Api-Key</c> scheme for the Python worker ingest.
///   4. Authorization policies, including the global JWT policy that
///      enforces the <see cref="RevokedTokenRequirement"/>.
///
/// The lockout policy and JWT validation parameters are explicit (not
/// relying on framework defaults) so a future framework upgrade cannot
/// silently weaken account-lockout behaviour or token validation.
/// </summary>
public static class AuthExtensions
{
    public static WebApplicationBuilder AddVortexFlowAuth(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ITokenService, TokenService>();
        builder.Services.AddSingleton<ITokenRevocationStore, RedisTokenRevocationStore>();
        builder.Services.AddScoped<ITenantProvider, ClaimsTenantProvider>();
        builder.Services.AddSingleton<ISecurityAuditLogger, SecurityAuditLogger>();
        builder.Services.AddSingleton<IAuthorizationHandler, RevokedTokenHandler>();

        builder.Services.AddIdentity<User, IdentityRole>(o =>
            {
                o.Password.RequireDigit = true;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequiredLength = 12;
                o.Password.RequireUppercase = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;
                o.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<VortexFlowDbContext>()
            .AddDefaultTokenProviders();

        AddAuthenticationSchemes(builder);
        AddAuthorizationPolicies(builder);

        return builder;
    }

    /// <summary>
    /// Wires both authentication schemes on a single
    /// <see cref="AuthenticationBuilder"/> chain. Splitting them into two
    /// independent <c>services.AddAuthentication(...).AddScheme(...)</c> calls
    /// would fail: <c>AddScheme</c> is a method on the
    /// <see cref="AuthenticationBuilder"/> returned by <c>AddAuthentication</c>,
    /// not on <see cref="IServiceCollection"/>.
    /// </summary>
    private static void AddAuthenticationSchemes(WebApplicationBuilder builder)
    {
        var jwtSecret = builder.Configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.SaveToken = true;
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:ValidIssuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:ValidAudience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwtKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async ctx =>
                    {
                        var jti = ctx.Principal?.FindFirst("jti")?.Value;
                        if (string.IsNullOrEmpty(jti)) return;
                        var store = ctx.HttpContext.RequestServices
                            .GetRequiredService<ITokenRevocationStore>();
                        if (await store.IsRevokedAsync(jti, ctx.HttpContext.RequestAborted))
                        {
                            ctx.Fail("Token has been revoked.");
                        }
                    },
                };
            })
            .AddScheme<AuthenticationSchemeOptions, IngestApiKeyAuthenticationHandler>(
                IngestApiKeyAuthenticationHandler.SchemeName, _ => { });
    }

    private static void AddAuthorizationPolicies(WebApplicationBuilder builder)
    {
        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy("InternalApiKey", p =>
            {
                p.AuthenticationSchemes = new[] { IngestApiKeyAuthenticationHandler.SchemeName };
                p.RequireAuthenticatedUser();
            });
            // Default JWT policy also enforces the revocation store, so a
            // logged-out token cannot be replayed even if it is still
            // cryptographically valid.
            o.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new RevokedTokenRequirement())
                .Build();
        });
    }
}

