using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CacheController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CacheController> _logger;

    public CacheController(ICacheService cacheService, ILogger<CacheController> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("{key}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCachedValue(string key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "Cache key is required." });

            _logger.LogDebug("Retrieving cached value for key '{Key}'", key);

            var value = await _cacheService.GetAsync<object>(key);

            if (value is null)
            {
                _logger.LogDebug("Cache miss for key '{Key}'", key);
                return NotFound(new { key, error = "Key not found in cache." });
            }

            _logger.LogDebug("Cache hit for key '{Key}'", key);

            return Ok(new { key, value });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached value for key '{Key}'", key);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while retrieving the cached value." });
        }
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SetCachedValue([FromBody] SetCacheRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(request.Key))
                return BadRequest(new { error = "Cache key is required." });

            var expiry = request.TtlSeconds > 0
                ? TimeSpan.FromSeconds(request.TtlSeconds)
                : TimeSpan.FromMinutes(10);

            _logger.LogDebug("Setting cache key '{Key}' with TTL {TtlSeconds}s", request.Key, request.TtlSeconds);

            await _cacheService.SetAsync(request.Key, request.Value, expiry);

            _logger.LogInformation("Cache key '{Key}' set successfully, expires in {TtlSeconds}s", request.Key, request.TtlSeconds);

            return Ok(new
            {
                request.Key,
                ttlSeconds = request.TtlSeconds,
                expiresAt = DateTime.UtcNow.Add(expiry)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key '{Key}'", request.Key);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while setting the cache value." });
        }
    }

    [HttpDelete("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveCachedValue(string key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "Cache key is required." });

            _logger.LogDebug("Removing cache key '{Key}'", key);

            await _cacheService.RemoveAsync(key);

            _logger.LogInformation("Cache key '{Key}' removed successfully", key);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key '{Key}'", key);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while removing the cache value." });
        }
    }

    [HttpGet("exists/{key}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckKeyExists(string key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest(new { error = "Cache key is required." });

            _logger.LogDebug("Checking existence of cache key '{Key}'", key);

            var exists = await _cacheService.ExistsAsync(key);

            return Ok(new { key, exists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of cache key '{Key}'", key);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while checking the cache key." });
        }
    }

    [HttpPost("clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ClearCache()
    {
        try
        {
            _logger.LogWarning("Clearing entire cache");

            var commonKeys = new[] { "products:all", "product:", "orders:" };

            foreach (var keyPattern in commonKeys)
            {
                await _cacheService.RemoveAsync(keyPattern);
            }

            _logger.LogInformation("Cache cleared successfully");

            return Ok(new
            {
                message = "Cache cleared successfully.",
                clearedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while clearing the cache." });
        }
    }
}

public class SetCacheRequest
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public int TtlSeconds { get; set; } = 600;
}
