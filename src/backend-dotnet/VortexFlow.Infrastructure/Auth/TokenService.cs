using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using VortexFlow.Application.Auth;

namespace VortexFlow.Infrastructure.Auth;

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly SymmetricSecurityKey _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;

    public TokenService(IConfiguration config)
    {
        _config = config;
        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Missing Jwt:Secret configuration.");
        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes (256 bits).");
        }
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _issuer = config["Jwt:ValidIssuer"]
            ?? throw new InvalidOperationException("Missing Jwt:ValidIssuer configuration.");
        _audience = config["Jwt:ValidAudience"]
            ?? throw new InvalidOperationException("Missing Jwt:ValidAudience configuration.");
        _accessTokenMinutes = config.GetValue("Jwt:AccessTokenLifetimeMinutes", 15);
    }

    public SymmetricSecurityKey GetSigningKey() => _key;

    public AccessTokenResult IssueAccessToken(string userId, string userName, IEnumerable<string> roles, string? tenantId)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_accessTokenMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
        };
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }
        foreach (var r in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new AccessTokenResult(token, expires, jti);
    }

    public RefreshTokenResult IssueRefreshToken(string userId, string tokenId)
    {
        var days = _config.GetValue("Jwt:RefreshTokenLifetimeDays", 7);
        var expires = DateTime.UtcNow.AddDays(days);
        var random = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        return new RefreshTokenResult(random, tokenId, expires);
    }

    public ClaimsPrincipal? ValidateExpiredToken(string token)
    {
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ValidateLifetime = false, // we want to read expired tokens for refresh
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token, validation, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
