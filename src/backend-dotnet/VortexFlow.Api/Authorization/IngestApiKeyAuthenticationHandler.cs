using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VortexFlow.Api.Authorization;

/// <summary>
/// Authentication scheme for the internal <c>POST /api/trends/ingest</c> endpoint.
/// The Python worker must send its API key in the <c>X-Api-Key</c> header; the
/// expected value is resolved from <c>ApiKeys:Internal</c> at startup and stored
/// only in process memory. The header name is intentionally distinct from any
/// cookie or bearer header so it cannot be replayed against the user-auth surface.
/// </summary>
public sealed class IngestApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IngestApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string PolicyName = "InternalApiKey";

    private readonly IConfiguration _config;

    public IngestApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration config) : base(options, logger, encoder)
    {
        _config = config;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var presented) || string.IsNullOrWhiteSpace(presented))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var expected = _config["ApiKeys:Internal"];
        if (string.IsNullOrEmpty(expected))
        {
            Logger.LogError("ApiKeys:Internal is not configured. Refusing all ingest requests.");
            return Task.FromResult(AuthenticateResult.Fail("Ingest API key not configured."));
        }
        if (!CryptographicEquals(presented!, expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "internal-worker"),
            new Claim(ClaimTypes.Role, "Internal"),
        }, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
