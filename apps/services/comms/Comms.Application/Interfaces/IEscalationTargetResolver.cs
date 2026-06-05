using Comms.Application.DTOs;

namespace Comms.Application.Interfaces;

public interface IEscalationTargetResolver
{
    Task<EscalationTarget?> ResolveAsync(Guid tenantId, Guid conversationId, CancellationToken ct = default);
}
