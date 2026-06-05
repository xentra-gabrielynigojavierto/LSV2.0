using BuildingBlocks.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace Identity.Infrastructure.Services;

public class InMemoryPolicyEvaluationCache : IPolicyEvaluationCache
{
    private readonly IMemoryCache _cache;

    public InMemoryPolicyEvaluationCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<PolicyEvaluationResult?> GetAsync(string cacheKey, CancellationToken ct = default)
    {
        _cache.TryGetValue(cacheKey, out PolicyEvaluationResult? result);
        return Task.FromResult(result);
    }

    public Task SetAsync(string cacheKey, PolicyEvaluationResult result, TimeSpan ttl, CancellationToken ct = default)
    {
        _cache.Set(cacheKey, result, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken ct = default)
    {
        _cache.Remove(cacheKey);
        return Task.CompletedTask;
    }
}
