using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;

namespace CareConnect.Infrastructure.Repositories;

/// <summary>LSCC-01-004: EF Core write-only repository for BlockedProviderAccessLogs.</summary>
public sealed class BlockedAccessLogRepository : IBlockedAccessLogRepository
{
    private readonly CareConnectDbContext _db;

    public BlockedAccessLogRepository(CareConnectDbContext db) => _db = db;

    public async Task AddAsync(BlockedProviderAccessLog entry, CancellationToken ct = default)
    {
        _db.BlockedProviderAccessLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}
