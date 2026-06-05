using Flow.Application.Adapters.AuditAdapter;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// Safe-baseline audit adapter. Always available, never fails the request.
/// In Phase 3 this will be replaced (or wrapped) by a real client to the
/// platform Audit service. Records to ILogger so events are visible in dev
/// and forwarded by any structured-logging pipeline already in place.
/// </summary>
public sealed class LoggingAuditAdapter : IAuditAdapter
{
    private readonly ILogger<LoggingAuditAdapter> _log;

    public LoggingAuditAdapter(ILogger<LoggingAuditAdapter> log) => _log = log;

    public Task WriteEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _log.LogInformation(
            "[audit] action={Action} entity={EntityType}:{EntityId} tenant={TenantId} user={UserId} desc={Description}",
            auditEvent.Action, auditEvent.EntityType, auditEvent.EntityId,
            auditEvent.TenantId, auditEvent.UserId, auditEvent.Description);
        return Task.CompletedTask;
    }
}
