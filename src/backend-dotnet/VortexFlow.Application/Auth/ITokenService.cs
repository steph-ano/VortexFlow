using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace VortexFlow.Application.Auth;

public interface ITokenService
{
    AccessTokenResult IssueAccessToken(string userId, string userName, IEnumerable<string> roles, string? tenantId);
    RefreshTokenResult IssueRefreshToken(string userId, string tokenId);
    ClaimsPrincipal? ValidateExpiredToken(string token);
    SymmetricSecurityKey GetSigningKey();
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt, string Jti);
public sealed record RefreshTokenResult(string Token, string TokenId, DateTime ExpiresAt);
