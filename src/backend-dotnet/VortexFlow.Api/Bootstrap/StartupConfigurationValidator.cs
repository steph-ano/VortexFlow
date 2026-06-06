using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace VortexFlow.Api.Bootstrap;

/// <summary>
/// Fails-fast validator for production-critical configuration. Executed at host
/// startup, BEFORE any DI wiring or service registration runs, so the container
/// never boots with a half-known secret.
///
/// <para>What is enforced (and why).</para>
/// <list type="bullet">
///   <item><c>Jwt:Secret</c> — must be present, non-empty, ≥ 32 chars, and must
///         not match a known placeholder string. A short or placeholder secret
///         would allow trivial JWT forgery.</item>
///   <item><c>ConnectionStrings:Postgres</c> / <c>Redis</c> / <c>RabbitMq</c> —
///         must be present. The RabbitMQ string must start with <c>amqp://</c>
///         or <c>amqps://</c> to catch copy-paste errors.</item>
///   <item><c>ApiKeys:Internal</c> — must be present and ≥ 32 chars; this key
///         authenticates the Python worker against the .NET ingest endpoint.</item>
///   <item><c>Bootstrap:AdminEmail</c> / <c>Bootstrap:AdminPassword</c> — only
///         required when <c>Bootstrap:SeedAdmin=true</c>; the password must be
///         ≥ 12 chars to match the Identity password policy.</item>
/// </list>
///
/// All errors are accumulated and reported in a single
/// <see cref="InvalidOperationException"/> so an operator can fix every missing
/// key in one pass instead of redeploying repeatedly.
/// </summary>
public static class StartupConfigurationValidator
{
    private const int MinSecretLength = 32;
    private const int MinAdminPasswordLength = 12;

    private static readonly string[] BannedJwtSubstrings =
    {
        "dev-only", "change_me", "changeme", "placeholder", "replace_me",
        "vortexflowsupersecret", "do-not-use", "example"
    };

    public static void Validate(IConfiguration configuration, IHostEnvironment env)
    {
        var errors = new List<string>();

        ValidateJwtSecret(configuration, errors);
        ValidateConnectionStrings(configuration, errors);
        ValidateApiKeys(configuration, errors);
        ValidateBootstrapAdmin(configuration, errors);

        if (errors.Count == 0)
        {
            return;
        }

        var banner = "FATAL: VortexFlow.Api cannot start. The following configuration " +
                     $"errors were detected in environment '{env.EnvironmentName}':";
        var message = banner + Environment.NewLine + string.Join(Environment.NewLine,
            errors.Select((e, i) => $"  {i + 1}. {e}"));

        // Log to stderr (host builder is not yet built) and throw.
        Console.Error.WriteLine(message);
        throw new InvalidOperationException(message);
    }

    private static void ValidateJwtSecret(IConfiguration cfg, List<string> errors)
    {
        var jwtSecret = cfg["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            errors.Add("Jwt:Secret is not configured. Set the env var 'Jwt__Secret' " +
                       "(e.g. 'openssl rand -base64 48') before starting the service.");
            return;
        }
        if (jwtSecret.Length < MinSecretLength)
        {
            errors.Add($"Jwt:Secret must be at least {MinSecretLength} characters " +
                       $"for HMAC-SHA256 (current length: {jwtSecret.Length}).");
            return;
        }
        if (BannedJwtSubstrings.Any(b => jwtSecret.Contains(b, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Jwt:Secret looks like a placeholder/dev value. " +
                       "Generate a fresh cryptographically random secret and inject it via env.");
        }
    }

    private static void ValidateConnectionStrings(IConfiguration cfg, List<string> errors)
    {
        var pg = cfg.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(pg))
        {
            errors.Add("ConnectionStrings:Postgres is not configured. " +
                       "Set the env var 'ConnectionStrings__Postgres' " +
                       "(e.g. 'Host=db;Port=5432;Database=vortexflow;Username=...;Password=...').");
        }

        var redis = cfg.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            errors.Add("ConnectionStrings:Redis is not configured. " +
                       "Set the env var 'ConnectionStrings__Redis' " +
                       "(e.g. 'redis:6379,password=...').");
        }

        var rabbit = cfg.GetConnectionString("RabbitMq");
        if (string.IsNullOrWhiteSpace(rabbit))
        {
            errors.Add("ConnectionStrings:RabbitMq is not configured. " +
                       "Set the env var 'ConnectionStrings__RabbitMq' " +
                       "(e.g. 'amqp://user:pass@host:5672/').");
        }
        else if (!rabbit.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase) &&
                 !rabbit.StartsWith("amqps://", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("ConnectionStrings:RabbitMq must start with 'amqp://' or 'amqps://'.");
        }
    }

    private static void ValidateApiKeys(IConfiguration cfg, List<string> errors)
    {
        var internalKey = cfg["ApiKeys:Internal"];
        if (string.IsNullOrWhiteSpace(internalKey))
        {
            errors.Add("ApiKeys:Internal is not configured. " +
                       "Set the env var 'ApiKeys__Internal' " +
                       "to a random string of at least 32 characters.");
            return;
        }
        if (internalKey.Length < MinSecretLength)
        {
            errors.Add("ApiKeys:Internal must be at least 32 characters " +
                       $"(current length: {internalKey.Length}).");
        }
    }

    private static void ValidateBootstrapAdmin(IConfiguration cfg, List<string> errors)
    {
        var seedAdminRaw = cfg["Bootstrap:SeedAdmin"];
        var seedAdmin = bool.TryParse(seedAdminRaw, out var s) && s;
        if (!seedAdmin)
        {
            return;
        }

        var adminEmail = cfg["Bootstrap:AdminEmail"];
        var adminPassword = cfg["Bootstrap:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            errors.Add("Bootstrap:AdminEmail is required when Bootstrap:SeedAdmin=true. " +
                       "Set the env var 'Bootstrap__AdminEmail'.");
        }
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            errors.Add("Bootstrap:AdminPassword is required when Bootstrap:SeedAdmin=true. " +
                       "Set the env var 'Bootstrap__AdminPassword' (never commit a real value).");
            return;
        }
        if (adminPassword.Length < MinAdminPasswordLength)
        {
            errors.Add("Bootstrap:AdminPassword must be at least 12 characters.");
        }
    }
}
