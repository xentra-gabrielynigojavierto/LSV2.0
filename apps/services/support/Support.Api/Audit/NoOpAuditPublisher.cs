using Microsoft.Extensions.Options;

namespace Support.Api.Audit;

/// <summary>
/// No-op audit publisher used when audit dispatch is disabled or
/// unconfigured. Logs the would-be record at Information level for
/// traceability — no Audit Service traffic.
/// </summary>
public sealed class NoOpAuditPublisher : IAuditPublisher
{
    private readonly ILogger<NoOpAuditPublisher> _log;
    private readonly IOptionsMonitor<AuditOptions> _options;

    public NoOpAuditPublisher(
        ILogger<NoOpAuditPublisher> log,
        IOptionsMonitor<AuditOptions> options)
    {
        _log = log;
        _options = options;
    }

    public Task PublishAsync(SupportAuditEvent auditEvent, CancellationToken ct = default)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _log.LogDebug(
                "Audit disabled; skipping dispatch event={EventType} resource={ResourceId}",
                auditEvent.EventType, auditEvent.ResourceId);
            return Task.CompletedTask;
        }

        _log.LogInformation(
            "[NoOpAuditPublisher] event={EventType} tenant={TenantId} actor={ActorUserId} resource={ResourceType}/{ResourceId} action={Action} outcome={Outcome}",
            auditEvent.EventType,
            auditEvent.TenantId,
            auditEvent.ActorUserId,
            auditEvent.ResourceType,
            auditEvent.ResourceId,
            auditEvent.Action,
            auditEvent.Outcome);
        return Task.CompletedTask;
    }
}
