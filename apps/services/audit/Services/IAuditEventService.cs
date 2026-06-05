using PlatformAuditEventService.DTOs;

namespace PlatformAuditEventService.Services;

public interface IAuditEventService
{
    Task<AuditEventResponse>          IngestAsync(IngestAuditEventRequest request, CancellationToken ct = default);
    Task<AuditEventResponse?>         GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AuditEventResponse>> QueryAsync(AuditEventQueryRequest query, CancellationToken ct = default);
    Task<long>                        CountAsync(CancellationToken ct = default);
}
