using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace OrderFlow.Api.Services
{
    public class CacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<CacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public CacheService(IConnectionMultiplexer redis, ILogger<CacheService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        private IDatabase GetDatabase() => _redis.GetDatabase();

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var db = GetDatabase();
                var value = await db.StringGetAsync(key);

                if (value.IsNullOrEmpty)
                {
                    _logger.LogDebug("Cache miss for key '{Key}'", key);
                    return default;
                }

                var result = JsonSerializer.Deserialize<T>(value!.ToString()!, _jsonOptions)!;
                _logger.LogDebug("Cache hit for key '{Key}'", key);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error while getting key '{Key}' from cache", key);
                return default;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize cache value for key '{Key}'", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var db = GetDatabase();
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);

                await db.StringSetAsync(key, serializedValue, expiry);
                _logger.LogInformation("Key '{Key}' set in cache, expiry: {Expiry}", key, expiry?.ToString() ?? "none");
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error while setting key '{Key}' in cache", key);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to serialize value for cache key '{Key}'", key);
                throw;
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                var db = GetDatabase();
                await db.KeyDeleteAsync(key);
                _logger.LogInformation("Key '{Key}' removed from cache", key);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error while removing key '{Key}' from cache", key);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var db = GetDatabase();
                var exists = await db.KeyExistsAsync(key);
                _logger.LogDebug("Key '{Key}' exists in cache: {Exists}", key, exists);
                return exists;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error while checking existence of key '{Key}' in cache", key);
                return false;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            try
            {
                var cached = await GetAsync<T>(key);
                if (cached is not null)
                {
                    return cached;
                }

                _logger.LogDebug("Cache miss for key '{Key}', executing factory", key);
                var value = await factory();

                if (value is not null)
                {
                    await SetAsync(key, value, expiry);
                }

                return value;
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Redis unavailable for key '{Key}', falling back to factory", key);
                return await factory();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetAsync for key '{Key}', falling back to factory", key);
                return await factory();
            }
        }
    }
}
