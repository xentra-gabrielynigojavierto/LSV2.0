using Comms.Domain.Entities;

namespace Comms.Application.Repositories;

public interface IQueueEscalationConfigRepository
{
    Task<QueueEscalationConfig?> GetByQueueAsync(Guid tenantId, Guid queueId, CancellationToken ct = default);
    Task<QueueEscalationConfig?> GetActiveByQueueAsync(Guid tenantId, Guid queueId, CancellationToken ct = default);
    Task AddAsync(QueueEscalationConfig config, CancellationToken ct = default);
    Task UpdateAsync(QueueEscalationConfig config, CancellationToken ct = default);
}
