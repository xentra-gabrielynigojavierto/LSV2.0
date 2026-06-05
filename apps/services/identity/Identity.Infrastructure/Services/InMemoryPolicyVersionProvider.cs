using System.Collections.Concurrent;
using BuildingBlocks.Authorization;

namespace Identity.Infrastructure.Services;

public class InMemoryPolicyVersionProvider : IPolicyVersionProvider
{
    private long _version;
    private readonly ConcurrentDictionary<string, long> _tenantVersions = new(StringComparer.OrdinalIgnoreCase);

    public long CurrentVersion => Interlocked.Read(ref _version);

    public void Increment()
    {
        Interlocked.Increment(ref _version);
    }

    public long GetVersion(string? tenantId = null)
    {
        if (string.IsNullOrEmpty(tenantId))
            return CurrentVersion;

        return _tenantVersions.GetOrAdd(tenantId, 0);
    }

    public void IncrementVersion(string? tenantId = null)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            Increment();
            return;
        }

        _tenantVersions.AddOrUpdate(tenantId, 1, (_, v) => v + 1);
    }

    public bool IsHealthy => true;
    public bool IsFrozen => false;
}
