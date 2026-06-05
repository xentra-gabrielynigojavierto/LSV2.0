using Support.Api.Audit;

namespace Support.Tests;

/// <summary>
/// Test-only audit publisher: captures every emitted audit event for
/// assertion. Can be flipped into a "throwing" mode to simulate transport
/// failure and verify that Support write paths absorb dispatch errors.
/// Thread-safe for parallel xUnit runs.
/// </summary>
public sealed class RecordingAuditPublisher : IAuditPublisher
{
    private readonly object _gate = new();
    private readonly List<SupportAuditEvent> _all = new();

    public bool ThrowOnPublish { get; set; }

    public Task PublishAsync(SupportAuditEvent auditEvent, CancellationToken ct = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("Simulated audit dispatch failure");
        }
        lock (_gate)
        {
            _all.Add(auditEvent);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<SupportAuditEvent> All
    {
        get
        {
            lock (_gate) return _all.ToList();
        }
    }

    public IReadOnlyList<SupportAuditEvent> ForTenant(string tenantId)
    {
        lock (_gate)
        {
            return _all.Where(e => e.TenantId == tenantId).ToList();
        }
    }

    public IReadOnlyList<SupportAuditEvent> ForResource(string resourceId)
    {
        lock (_gate)
        {
            return _all.Where(e => e.ResourceId == resourceId).ToList();
        }
    }

    public IReadOnlyList<SupportAuditEvent> OfType(string eventType)
    {
        lock (_gate)
        {
            return _all.Where(e => e.EventType == eventType).ToList();
        }
    }

    public void Clear()
    {
        lock (_gate) _all.Clear();
    }
}
