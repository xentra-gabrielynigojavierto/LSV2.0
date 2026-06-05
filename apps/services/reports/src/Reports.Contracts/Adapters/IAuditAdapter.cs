using Reports.Contracts.Audit;
using Reports.Contracts.Context;

namespace Reports.Contracts.Adapters;

public interface IAuditAdapter
{
    Task<AdapterResult<bool>> RecordEventAsync(AuditEventDto auditEvent, CancellationToken ct = default);

    bool IsRealIntegration { get; }
}
