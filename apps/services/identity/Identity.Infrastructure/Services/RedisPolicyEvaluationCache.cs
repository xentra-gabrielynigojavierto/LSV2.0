using System.Text.Json;
using BuildingBlocks.Authorization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Identity.Infrastructure.Services;

public class RedisPolicyEvaluationCache : IPolicyEvaluationCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisPolicyEvaluationCache> _logger;

    public RedisPolicyEvaluationCache(
        IConnectionMultiplexer redis,
        ILogger<RedisPolicyEvaluationCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<PolicyEvaluationResult?> GetAsync(string cacheKey, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var val = await db.StringGetAsync(cacheKey);
            if (val.IsNullOrEmpty)
                return null;

            var result = JsonSerializer.Deserialize<PolicyEvaluationResult>(val!, JsonOptions);
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed policy cache entry for key {CacheKey} — ignoring", cacheKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis policy cache read failed for key {CacheKey} — fail open", cacheKey);
            return null;
        }
    }

    public async Task SetAsync(string cacheKey, PolicyEvaluationResult result, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(result, JsonOptions);
            await db.StringSetAsync(cacheKey, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis policy cache write failed for key {CacheKey} — continuing without cache", cacheKey);
        }
    }

    public async Task RemoveAsync(string cacheKey, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis policy cache remove failed for key {CacheKey}", cacheKey);
        }
    }
}
