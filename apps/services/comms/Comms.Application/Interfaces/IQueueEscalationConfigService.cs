using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IQueueEscalationConfigService
{
    Task<QueueEscalationConfigResponse> CreateOrUpdateAsync(Guid tenantId, Guid queueId, CreateQueueEscalationConfigRequest request, Guid userId, CancellationToken ct = default);
    Task<QueueEscalationConfigResponse> UpdateAsync(Guid tenantId, Guid queueId, UpdateQueueEscalationConfigRequest request, Guid userId, CancellationToken ct = default);
    Task<QueueEscalationConfigResponse?> GetByQueueAsync(Guid tenantId, Guid queueId, CancellationToken ct = default);
}
