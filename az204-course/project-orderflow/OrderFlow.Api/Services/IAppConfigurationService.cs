namespace OrderFlow.Api.Services;

public interface IAppConfigurationService
{
    Task<bool> GetFeatureFlagAsync(string flagName);
    Task SetFeatureFlagAsync(string flagName, bool isEnabled);
    Task<string?> GetSettingAsync(string key);
    Task<T> GetSettingAsync<T>(string key, T defaultValue);
}
