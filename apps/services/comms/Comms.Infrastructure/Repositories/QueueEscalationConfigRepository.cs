using Microsoft.EntityFrameworkCore;
using Comms.Application.Repositories;
using Comms.Domain.Entities;
using Comms.Infrastructure.Persistence;

namespace Comms.Infrastructure.Repositories;

public class QueueEscalationConfigRepository : IQueueEscalationConfigRepository
{
    private readonly CommsDbContext _db;

    public QueueEscalationConfigRepository(CommsDbContext db) => _db = db;

    public async Task<QueueEscalationConfig?> GetByQueueAsync(Guid tenantId, Guid queueId, CancellationToken ct = default)
        => await _db.QueueEscalationConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.QueueId == queueId, ct);

    public async Task<QueueEscalationConfig?> GetActiveByQueueAsync(Guid tenantId, Guid queueId, CancellationToken ct = default)
        => await _db.QueueEscalationConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.QueueId == queueId && c.IsActive, ct);

    public async Task AddAsync(QueueEscalationConfig config, CancellationToken ct = default)
    {
        _db.QueueEscalationConfigs.Add(config);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(QueueEscalationConfig config, CancellationToken ct = default)
    {
        _db.QueueEscalationConfigs.Update(config);
        await _db.SaveChangesAsync(ct);
    }
}
