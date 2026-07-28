using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Api.Services
{
    public class AppConfigurationService : IAppConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppConfigurationService> _logger;

        public AppConfigurationService(IConfiguration configuration, ILogger<AppConfigurationService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> GetFeatureFlagAsync(string featureName)
        {
            try
            {
                var key = $"FeatureManagement:{featureName}";
                var value = _configuration[key];

                if (string.IsNullOrEmpty(value))
                {
                    _logger.LogDebug("Feature flag '{FeatureName}' not found, defaulting to false", featureName);
                    return await Task.FromResult(false);
                }

                if (bool.TryParse(value, out var result))
                {
                    _logger.LogInformation("Feature flag '{FeatureName}' is set to {Value}", featureName, result);
                    return await Task.FromResult(result);
                }

                _logger.LogWarning("Feature flag '{FeatureName}' has invalid boolean value '{Value}', defaulting to false", featureName, value);
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read feature flag '{FeatureName}' from configuration", featureName);
                throw;
            }
        }

        public Task SetFeatureFlagAsync(string featureName, bool isEnabled)
        {
            try
            {
                _logger.LogWarning(
                    "SetFeatureFlagAsync called for '{FeatureName}' with value {Value}. " +
                    "Note: Runtime feature flag modification requires Azure App Configuration SDK write support. " +
                    "This is a placeholder implementation.",
                    featureName,
                    isEnabled);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set feature flag '{FeatureName}' in configuration", featureName);
                throw;
            }
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var value = _configuration[key];

                if (string.IsNullOrEmpty(value))
                {
                    _logger.LogWarning("Setting '{Key}' not found or empty in configuration", key);
                    throw new KeyNotFoundException($"Setting '{key}' not found or empty in configuration.");
                }

                _logger.LogInformation("Setting '{Key}' retrieved from configuration", key);
                return await Task.FromResult(value);
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read setting '{Key}' from configuration", key);
                throw;
            }
        }

        public async Task<T> GetSettingAsync<T>(string key, T defaultValue)
        {
            try
            {
                var value = _configuration[key];

                if (string.IsNullOrEmpty(value))
                {
                    _logger.LogDebug("Setting '{Key}' not found, returning default value", key);
                    return await Task.FromResult(defaultValue);
                }

                var convertedValue = (T)Convert.ChangeType(value, typeof(T));
                _logger.LogInformation("Setting '{Key}' retrieved from configuration as {Type}", key, typeof(T).Name);
                return await Task.FromResult(convertedValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read setting '{Key}' as type {Type} from configuration", key, typeof(T).Name);
                return await Task.FromResult(defaultValue);
            }
        }
    }
}
