using Support.Api.Notifications;

namespace Support.Tests;

/// <summary>
/// Test-only notification publisher: captures every emitted notification
/// for assertion, and can be flipped into a "throwing" mode to simulate
/// transport failure. Thread-safe for parallel xUnit runs.
/// </summary>
public sealed class RecordingNotificationPublisher : INotificationPublisher
{
    private readonly object _gate = new();
    private readonly List<SupportNotification> _all = new();

    public bool ThrowOnPublish { get; set; }

    public Task PublishAsync(SupportNotification notification, CancellationToken ct = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("Simulated notification dispatch failure");
        }
        lock (_gate)
        {
            _all.Add(notification);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<SupportNotification> All
    {
        get
        {
            lock (_gate) return _all.ToList();
        }
    }

    public IReadOnlyList<SupportNotification> ForTenant(string tenantId)
    {
        lock (_gate)
        {
            return _all.Where(n => n.TenantId == tenantId).ToList();
        }
    }

    public IReadOnlyList<SupportNotification> ForTicket(Guid ticketId)
    {
        lock (_gate)
        {
            return _all.Where(n => n.TicketId == ticketId).ToList();
        }
    }

    public void Clear()
    {
        lock (_gate) _all.Clear();
    }
}
