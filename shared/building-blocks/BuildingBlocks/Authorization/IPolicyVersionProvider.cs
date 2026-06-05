namespace BuildingBlocks.Authorization;

public interface IPolicyVersionProvider
{
    long CurrentVersion { get; }
    void Increment();

    long GetVersion(string? tenantId = null);
    void IncrementVersion(string? tenantId = null);

    bool IsHealthy { get; }
    bool IsFrozen { get; }
}
