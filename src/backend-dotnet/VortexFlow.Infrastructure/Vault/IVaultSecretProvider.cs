namespace VortexFlow.Infrastructure.Vault;

public interface IVaultSecretProvider
{
    Task<string> GetSecretAsync(string key);
}
