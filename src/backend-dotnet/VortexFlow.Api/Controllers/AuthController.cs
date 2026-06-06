using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
using VortexFlow.Application.Audit;
using VortexFlow.Application.Auth;
using VortexFlow.Application.Tenancy;
using VortexFlow.Domain.Entities;
using VortexFlow.Domain.Exceptions;
// Disambiguate: System.ComponentModel.DataAnnotations also defines ValidationException.
// The application-wide domain exception must take precedence so ProblemDetailsExceptionMiddleware
// can map it to RFC 7807.
using ValidationException = VortexFlow.Domain.Exceptions.ValidationException;

namespace VortexFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const int LockoutMaxFailedAccessAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ITokenRevocationStore _revocationStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly ISecurityAuditLogger _audit;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _refreshTtl;

    public AuthController(
        SignInManager<User> signInManager,
        UserManager<User> userManager,
        ITokenService tokenService,
        ITokenRevocationStore revocationStore,
        ITenantProvider tenantProvider,
        ISecurityAuditLogger audit,
        IConnectionMultiplexer redis,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _tokenService = tokenService;
        _revocationStore = revocationStore;
        _tenantProvider = tenantProvider;
        _audit = audit;
        _redis = redis;
        _configuration = configuration;
        _refreshTtl = TimeSpan.FromDays(configuration.GetValue("Jwt:RefreshTokenLifetimeDays", 7));
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var errors = ValidateRequest(request);
        if (errors.Count > 0) throw new ValidationException(errors);

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            Name = request.Name,
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var mapped = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            throw new ValidationException(mapped);
        }
        await _userManager.AddToRoleAsync(user, "Viewer");

        _audit.SecurityEvent("user_registered", user.Id, new Dictionary<string, object?> { ["email"] = request.Email });
        return Ok(new { Message = "User registered successfully" });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var errors = ValidateRequest(request);
        if (errors.Count > 0) throw new ValidationException(errors);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _audit.LoginFailure(request.Email, "user_not_found", HttpContext.ClientIp());
            throw new ForbiddenException("Invalid credentials.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _audit.LoginFailure(request.Email, "locked_out", HttpContext.ClientIp());
            throw new ForbiddenException("Account is temporarily locked. Try again later.");
        }
        if (!result.Succeeded)
        {
            _audit.LoginFailure(request.Email, "bad_password", HttpContext.ClientIp());
            throw new ForbiddenException("Invalid credentials.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.IssueAccessToken(user.Id, user.UserName ?? user.Email!, roles, user.TenantId);
        var refresh = _tokenService.IssueRefreshToken(user.Id, Guid.NewGuid().ToString("N"));
        await StoreRefreshTokenAsync(user.Id, refresh);

        _audit.LoginSuccess(user.Id, user.Email!, HttpContext.ClientIp());
        Response.Cookies.Append("refresh_token", refresh.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = refresh.ExpiresAt,
            Path = "/api/auth",
        });
        return Ok(new
        {
            accessToken = accessToken.Token,
            expiresAt = accessToken.ExpiresAt,
            user = new { id = user.Id, email = user.Email, name = user.Name, roles },
        });
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            throw new ForbiddenException("Missing refresh token.");
        }
        var stored = await GetStoredRefreshTokenAsync(refreshToken);
        if (stored is null)
        {
            throw new ForbiddenException("Invalid refresh token.");
        }
        var user = await _userManager.FindByIdAsync(stored.UserId);
        if (user is null)
        {
            throw new ForbiddenException("User not found.");
        }
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.IssueAccessToken(user.Id, user.UserName ?? user.Email!, roles, user.TenantId);

        // Rotate the refresh token (single use).
        await RevokeRefreshTokenAsync(refreshToken);
        var newRefresh = _tokenService.IssueRefreshToken(user.Id, Guid.NewGuid().ToString("N"));
        await StoreRefreshTokenAsync(user.Id, newRefresh);
        Response.Cookies.Append("refresh_token", newRefresh.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = newRefresh.ExpiresAt,
            Path = "/api/auth",
        });
        return Ok(new
        {
            accessToken = accessToken.Token,
            expiresAt = accessToken.ExpiresAt,
        });
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!string.IsNullOrEmpty(jti))
        {
            var lifetime = _configuration.GetValue("Jwt:AccessTokenLifetimeMinutes", 15);
            await _revocationStore.RevokeAsync(jti, TimeSpan.FromMinutes(lifetime), _tenantProvider.GetCurrentUserId(), "logout", HttpContext.RequestAborted);
            _audit.TokenRevoked(_tenantProvider.GetCurrentUserId() ?? "unknown", jti, "logout", HttpContext.ClientIp());
        }
        if (Request.Cookies.TryGetValue("refresh_token", out var refresh))
        {
            await RevokeRefreshTokenAsync(refresh);
        }
        Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Me()
    {
        var userId = _tenantProvider.GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) throw new ForbiddenException("Unauthenticated.");
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) throw new NotFoundException("User", userId);
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            name = user.Name,
            roles,
        });
    }

    private static IDictionary<string, string[]> ValidateRequest(RegisterRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["body"] = new[] { "Request body is required." };
            return errors;
        }
        if (string.IsNullOrWhiteSpace(request.Email))
            errors["email"] = new[] { "email is required" };
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            errors["password"] = new[] { "password must be at least 12 characters" };
        if (request is RegisterRequest reg && string.IsNullOrWhiteSpace(reg.Name))
            errors["name"] = new[] { "name is required" };
        return errors;
    }

    private static IDictionary<string, string[]> ValidateRequest(LoginRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["body"] = new[] { "Request body is required." };
            return errors;
        }
        if (string.IsNullOrWhiteSpace(request.Email)) errors["email"] = new[] { "email is required" };
        if (string.IsNullOrWhiteSpace(request.Password)) errors["password"] = new[] { "password is required" };
        return errors;
    }

    private const string RefreshKeyPrefix = "refresh:token:";
    private async Task StoreRefreshTokenAsync(string userId, RefreshTokenResult token)
    {
        var db = _redis.GetDatabase();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            UserId = userId,
            TokenId = token.TokenId,
            IssuedAt = DateTime.UtcNow,
        });
        await db.StringSetAsync(RefreshKeyPrefix + token.Token, payload, _refreshTtl);
    }

    private async Task<RefreshTokenRecord?> GetStoredRefreshTokenAsync(string token)
    {
        var db = _redis.GetDatabase();
        var raw = await db.StringGetAsync(RefreshKeyPrefix + token);
        if (raw.IsNullOrEmpty) return null;
        return System.Text.Json.JsonSerializer.Deserialize<RefreshTokenRecord>(raw!);
    }

    private async Task RevokeRefreshTokenAsync(string token)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(RefreshKeyPrefix + token);
    }

    private record RefreshTokenRecord(string UserId, string TokenId, DateTime IssuedAt);
}

public class RegisterRequest
{
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}
