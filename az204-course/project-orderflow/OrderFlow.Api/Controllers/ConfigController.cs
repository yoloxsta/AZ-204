using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IAppConfigurationService _configService;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<ConfigController> _logger;

    private static readonly string[] DefaultFeatureFlags =
    [
        "EnableNewCheckout",
        "EnableBetaFeatures",
        "EnableAdvancedAnalytics",
        "EnableLoyaltyProgram",
        "EnableInternationalShipping",
        "EnableRealTimeInventory",
        "EnableOrderNotifications",
        "EnableDarkMode"
    ];

    public ConfigController(
        IAppConfigurationService configService,
        IKeyVaultService keyVaultService,
        ILogger<ConfigController> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _keyVaultService = keyVaultService ?? throw new ArgumentNullException(nameof(keyVaultService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("features/{flagName}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFeatureFlag(string flagName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(flagName))
                return BadRequest(new { error = "Feature flag name is required." });

            _logger.LogInformation("Retrieving feature flag '{FlagName}'", flagName);

            var isEnabled = await _configService.GetFeatureFlagAsync(flagName);

            return Ok(new
            {
                flagName,
                isEnabled,
                checkedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feature flag '{FlagName}'", flagName);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while retrieving the feature flag." });
        }
    }

    [HttpGet("features")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllFeatureFlags()
    {
        try
        {
            _logger.LogInformation("Retrieving all feature flags");

            var flags = new Dictionary<string, bool>();

            foreach (var flagName in DefaultFeatureFlags)
            {
                try
                {
                    var isEnabled = await _configService.GetFeatureFlagAsync(flagName);
                    flags[flagName] = isEnabled;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve feature flag '{FlagName}', defaulting to false", flagName);
                    flags[flagName] = false;
                }
            }

            return Ok(new
            {
                featureFlags = flags,
                totalCount = flags.Count,
                checkedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feature flags");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while retrieving feature flags." });
        }
    }

    [HttpGet("settings/{key}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSetting(string key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "Setting key is required." });

            _logger.LogInformation("Retrieving configuration setting '{Key}'", key);

            var value = await _configService.GetSettingAsync(key);

            if (value is null)
            {
                _logger.LogWarning("Configuration setting '{Key}' not found", key);
                return NotFound(new { error = $"Setting '{key}' not found." });
            }

            return Ok(new
            {
                key,
                value,
                retrievedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration setting '{Key}'", key);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while retrieving the configuration setting." });
        }
    }

    [HttpGet("keyvault/{secretName}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetKeyVaultSecret(string secretName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(secretName))
                return BadRequest(new { error = "Secret name is required." });

            _logger.LogInformation("Retrieving secret '{SecretName}' from Key Vault", secretName);

            var secretValue = await _keyVaultService.GetSecretAsync(secretName);

            _logger.LogInformation("Secret '{SecretName}' retrieved successfully from Key Vault", secretName);

            return Ok(new
            {
                secretName,
                retrievedAt = DateTime.UtcNow
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Secret '{SecretName}' not found in Key Vault", secretName);
            return NotFound(new { error = $"Secret '{secretName}' not found in Key Vault." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret '{SecretName}' from Key Vault", secretName);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while retrieving the secret from Key Vault." });
        }
    }
}
