using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Audit;

namespace Reports.Infrastructure.Adapters;

public sealed class MockAuditAdapter : IAuditAdapter
{
    private readonly ILogger<MockAuditAdapter> _log;

    public MockAuditAdapter(ILogger<MockAuditAdapter> log) => _log = log;

    public bool IsRealIntegration => false;

    public Task<AdapterResult<bool>> RecordEventAsync(AuditEventDto auditEvent, CancellationToken ct)
    {
        _log.LogInformation(
            "MockAuditAdapter: [{EventType}] tenant={TenantId} user={ActorUserId} entity={EntityType}/{EntityId} — {Description} [Correlation={CorrelationId}]",
            auditEvent.EventType,
            auditEvent.TenantId,
            auditEvent.ActorUserId,
            auditEvent.EntityType,
            auditEvent.EntityId,
            auditEvent.Description,
            auditEvent.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(true));
    }
}
