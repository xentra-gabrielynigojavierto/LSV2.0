namespace BuildingBlocks.Authorization;

public interface IPolicyEvaluationCache
{
    Task<PolicyEvaluationResult?> GetAsync(string cacheKey, CancellationToken ct = default);
    Task SetAsync(string cacheKey, PolicyEvaluationResult result, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string cacheKey, CancellationToken ct = default);
}
