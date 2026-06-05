using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

/// <summary>LSCC-01-004: Write-only repository for blocked-access log events.</summary>
public interface IBlockedAccessLogRepository
{
    Task AddAsync(BlockedProviderAccessLog entry, CancellationToken ct = default);
}
