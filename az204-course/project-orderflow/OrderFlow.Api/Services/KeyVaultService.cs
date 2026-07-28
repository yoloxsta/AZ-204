using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Api.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<KeyVaultService> _logger;

    public KeyVaultService(SecretClient secretClient, ILogger<KeyVaultService> logger)
    {
        _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(secretName);
            _logger.LogInformation("Secret '{SecretName}' retrieved", secretName);
            return response.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Secret '{secretName}' not found.", ex);
        }
    }

    public async Task SetSecretAsync(string secretName, string secretValue)
    {
        try
        {
            var secret = new KeyVaultSecret(secretName, secretValue);
            await _secretClient.SetSecretAsync(secret);
            _logger.LogInformation("Secret '{SecretName}' set", secretName);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to set secret '{SecretName}'", secretName);
            throw;
        }
    }

    public async Task DeleteSecretAsync(string secretName)
    {
        try
        {
            var operation = await _secretClient.StartDeleteSecretAsync(secretName);
            await operation.WaitForCompletionAsync();
            _logger.LogInformation("Secret '{SecretName}' deleted", secretName);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Secret '{secretName}' not found.", ex);
        }
    }

    public async Task<Dictionary<string, string>> GetAllSecretsAsync()
    {
        try
        {
            var secrets = new Dictionary<string, string>();
            await foreach (var props in _secretClient.GetPropertiesOfSecretsAsync())
            {
                try
                {
                    var secret = await _secretClient.GetSecretAsync(props.Name);
                    secrets[props.Name] = secret.Value.Value;
                }
                catch (Azure.RequestFailedException)
                {
                    _logger.LogWarning("Could not retrieve value for secret '{Name}'", props.Name);
                }
            }
            return secrets;
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to list secrets");
            throw;
        }
    }
}
