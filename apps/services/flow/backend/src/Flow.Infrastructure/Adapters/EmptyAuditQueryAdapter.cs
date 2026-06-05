using Flow.Application.Adapters.AuditAdapter;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// E13.1 — safe-baseline read-only audit query adapter. Always
/// available; returns an empty event list. Used when
/// <c>Audit:BaseUrl</c> is not configured (local dev, integration
/// tests) and as the fallback that the HTTP adapter degrades to when
/// the audit service is unreachable.
/// </summary>
public sealed class EmptyAuditQueryAdapter : IAuditQueryAdapter
{
    private readonly ILogger<EmptyAuditQueryAdapter> _log;

    public EmptyAuditQueryAdapter(ILogger<EmptyAuditQueryAdapter> log) => _log = log;

    public Task<AuditEventFetchResult> GetEventsForEntityAsync(
        string entityType,
        string entityId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        _log.LogDebug(
            "[audit-query] empty adapter — returning [] for entity={EntityType}:{EntityId} tenant={TenantId}",
            entityType, entityId, tenantId);
        return Task.FromResult(new AuditEventFetchResult(Array.Empty<AuditEventRecord>(), Truncated: false));
    }
}
