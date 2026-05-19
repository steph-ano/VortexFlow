using Microsoft.Extensions.Configuration;

namespace VortexFlow.Infrastructure.Vault;

public class EnvironmentVaultSecretProvider : IVaultSecretProvider
{
    private readonly IConfiguration _configuration;

    public EnvironmentVaultSecretProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> GetSecretAsync(string key)
    {
        var secret = Environment.GetEnvironmentVariable(key) ?? _configuration[key];
        
        if (string.IsNullOrEmpty(secret))
        {
            throw new Exception($"Secret '{key}' not found in environment or configuration.");
        }

        return Task.FromResult(secret);
    }
}
